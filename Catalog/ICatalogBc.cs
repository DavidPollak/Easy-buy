using NCR.RetailGateway.DataContracts;
using NCR.RetailGateway.DataContracts.Catalog;
using NCR.RetailGateway.DataContracts.Promotions.Requests;
using NCR.RetailGateway.Model.Catalog;

namespace NCR.RetailGateway.BusinessComponents.Catalog
{
    public interface ICatalogBc
    {
        ItemSearchResponse GetItemsBySearchText(SearchRequest searchRequest);
        ItemLookupResponse GetItemLookup(string storeId, string itemId);
        ItemsLookupResponse GetItemsLookup(string storeId, string[] barcodes);
        PromotionLookupResponse GetPromotionById(string storeId, string promotionId);
        PromotionLookupResponse GetPromotionsByItemId(string storeId, string itemId);
        SalesTransactionReponse PromotionApproval(string storeId, string transactionId, PromotionApprovalRequest promotionApprovalRequest);
    }
}
