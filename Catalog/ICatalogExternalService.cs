using NCR.RetailGateway.DataContracts.Catalog;

namespace NCR.RetailGateway.Model.Catalog
{
    public interface ICatalogExternalService
    {
        ItemSearchResponse GetItemsBySearchText(SearchRequest searchRequest);
        ItemSearchResponse GetRelativeItems(string storeId, string barcode);
        ItemLookupResponse GetItemLookup(string storeId, string barcode);        
        ItemsLookupResponse GetItemsLookup(string storeId, string[] barcodes);
    }
}
