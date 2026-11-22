using Catalog.API.Data;
using Catalog.API.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Endpoints;

/// <summary>
/// Samme stier og samme JSON som PublicApi's katalog-endpoints. Gatewayen kan derfor flytte /api/catalog-*
/// fra PublicApi hertil uden at ændre noget i BlazorAdmin. Alle må læse; opret, ret og slet kræver rollen
/// Administrators (politikken "admin"), præcis som i PublicApi.
/// </summary>
public static class CatalogEndpoints
{
    // Nye varer får standardbilledet, ligesom i PublicApi (upload af billeder er slået fra i eShopOnWeb).
    public const string DefaultPictureUri = PictureUriComposer.Placeholder + "/images/products/eCatalog-item-default.png";

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/catalog-items").WithTags("CatalogItems");
        items.MapGet("/", ListItems);
        items.MapGet("/{catalogItemId:int}", GetItem);
        items.MapPost("/", CreateItem).RequireAuthorization("admin");
        items.MapPut("/", UpdateItem).RequireAuthorization("admin");
        items.MapDelete("/{catalogItemId:int}", DeleteItem).RequireAuthorization("admin");

        app.MapGet("/api/catalog-brands", ListBrands).WithTags("CatalogBrands");
        app.MapGet("/api/catalog-types", ListTypes).WithTags("CatalogTypes");

        return app;
    }

    /// <summary>
    /// GET /api/catalog-items?pageSize=10&amp;pageIndex=0&amp;catalogBrandId=2&amp;catalogTypeId=1
    /// pageSize 0 (eller udeladt) giver alle varer. Web bruger desuden ?ids=1&amp;ids=5 til at hente bestemte varer
    /// (navn og billede til kurven og til ordren).
    /// </summary>
    public static async Task<Ok<ListPagedCatalogItemResponse>> ListItems(
        int? pageSize, int? pageIndex, int? catalogBrandId, int? catalogTypeId,
        [FromQuery(Name = "ids")] int[]? ids,
        CatalogDbContext db, PictureUriComposer pictures, CancellationToken cancellationToken)
    {
        var size = Math.Max(pageSize ?? 0, 0);
        var index = Math.Max(pageIndex ?? 0, 0);

        var query = db.CatalogItems.AsNoTracking()
            .Where(i => (!catalogBrandId.HasValue || i.CatalogBrandId == catalogBrandId)
                        && (!catalogTypeId.HasValue || i.CatalogTypeId == catalogTypeId));

        if (ids is { Length: > 0 } wanted)
        {
            query = query.Where(i => wanted.Contains(i.Id));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var page = query.OrderBy(i => i.Id).Skip(index * size);
        if (size > 0)
        {
            page = page.Take(size);
        }

        var items = await page.ToListAsync(cancellationToken);
        var pageCount = size > 0
            ? (int)Math.Ceiling(totalItems / (decimal)size)
            : (totalItems > 0 ? 1 : 0);

        return TypedResults.Ok(new ListPagedCatalogItemResponse(
            items.Select(i => ToDto(i, pictures)).ToList(), pageCount, totalItems));
    }

    public static async Task<Results<Ok<GetByIdCatalogItemResponse>, NotFound>> GetItem(
        int catalogItemId, CatalogDbContext db, PictureUriComposer pictures, CancellationToken cancellationToken)
    {
        var item = await db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == catalogItemId, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new GetByIdCatalogItemResponse(ToDto(item, pictures)));
    }

    public static async Task<Results<Created<CreateCatalogItemResponse>, Conflict<ErrorResponse>, BadRequest<ErrorResponse>>> CreateItem(
        CreateCatalogItemRequest request, CatalogDbContext db, PictureUriComposer pictures, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var name = request.Name ?? string.Empty;
        if (await db.CatalogItems.AnyAsync(i => i.Name == name, cancellationToken))
        {
            // Samme besked som PublicApi's DuplicateException.
            return TypedResults.Conflict(new ErrorResponse(StatusCodes.Status409Conflict, $"A catalogItem with name {name} already exists"));
        }

        var unknown = await FindUnknownBrandOrType(db, request.CatalogBrandId, request.CatalogTypeId, cancellationToken);
        if (unknown is not null)
        {
            return TypedResults.BadRequest(unknown);
        }

        CatalogItem item;
        try
        {
            item = new CatalogItem(request.CatalogTypeId, request.CatalogBrandId, request.Description ?? string.Empty,
                name, request.Price, DefaultPictureUri);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(StatusCodes.Status400BadRequest, ex.Message));
        }

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        loggerFactory.CreateLogger(typeof(CatalogEndpoints)).LogInformation("Catalog item {CatalogItemId} created", item.Id);
        return TypedResults.Created($"/api/catalog-items/{item.Id}", new CreateCatalogItemResponse(ToDto(item, pictures)));
    }

    public static async Task<Results<Ok<UpdateCatalogItemResponse>, NotFound, BadRequest<ErrorResponse>>> UpdateItem(
        UpdateCatalogItemRequest request, CatalogDbContext db, PictureUriComposer pictures, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var item = await db.CatalogItems.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        var unknown = await FindUnknownBrandOrType(db, request.CatalogBrandId, request.CatalogTypeId, cancellationToken);
        if (unknown is not null)
        {
            return TypedResults.BadRequest(unknown);
        }

        try
        {
            item.UpdateDetails(request.Name ?? string.Empty, request.Description ?? string.Empty, request.Price);
            item.UpdateBrand(request.CatalogBrandId);
            item.UpdateType(request.CatalogTypeId);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new ErrorResponse(StatusCodes.Status400BadRequest, ex.Message));
        }

        await db.SaveChangesAsync(cancellationToken);

        loggerFactory.CreateLogger(typeof(CatalogEndpoints)).LogInformation("Catalog item {CatalogItemId} updated", item.Id);
        return TypedResults.Ok(new UpdateCatalogItemResponse(ToDto(item, pictures)));
    }

    public static async Task<Results<Ok<DeleteCatalogItemResponse>, NotFound>> DeleteItem(
        int catalogItemId, CatalogDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var item = await db.CatalogItems.FirstOrDefaultAsync(i => i.Id == catalogItemId, cancellationToken);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        loggerFactory.CreateLogger(typeof(CatalogEndpoints)).LogInformation("Catalog item {CatalogItemId} deleted", catalogItemId);
        return TypedResults.Ok(new DeleteCatalogItemResponse("Deleted"));
    }

    public static async Task<Ok<ListCatalogBrandsResponse>> ListBrands(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var brands = await db.CatalogBrands.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new CatalogLookupDto(b.Id, b.Brand))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ListCatalogBrandsResponse(brands));
    }

    public static async Task<Ok<ListCatalogTypesResponse>> ListTypes(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var types = await db.CatalogTypes.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new CatalogLookupDto(t.Id, t.Type))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ListCatalogTypesResponse(types));
    }

    private static async Task<ErrorResponse?> FindUnknownBrandOrType(CatalogDbContext db, int brandId, int typeId,
        CancellationToken cancellationToken)
    {
        if (!await db.CatalogBrands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            return new ErrorResponse(StatusCodes.Status400BadRequest, $"Catalog brand {brandId} does not exist");
        }

        if (!await db.CatalogTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            return new ErrorResponse(StatusCodes.Status400BadRequest, $"Catalog type {typeId} does not exist");
        }

        return null;
    }

    private static CatalogItemDto ToDto(CatalogItem item, PictureUriComposer pictures) =>
        new(item.Id, item.Name, item.Description, item.Price, pictures.Compose(item.PictureUri),
            item.CatalogTypeId, item.CatalogBrandId);
}
