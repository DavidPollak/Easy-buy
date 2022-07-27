using System;
using System.Collections.Generic;
using System.Linq;
using NCR.RetailGateway.BusinessComponents.Framework;
using NCR.RetailGateway.DataContracts;
using NCR.RetailGateway.DataContracts.Catalog;
using NCR.RetailGateway.DataContracts.POS;
using NCR.RetailGateway.DataContracts.Promotions.Requests;
using NCR.RetailGateway.Model.Catalog;
using NCR.RetailGateway.Model.Data;
using NCR.RetailGateway.Model.Exceptions;
using NCR.RetailGateway.Model.Framework;
using NCR.RetailGateway.Model.Promotion;

namespace NCR.RetailGateway.BusinessComponents.Catalog
{
    public class CatalogBc : ICatalogBc
    {
        private readonly IExternalServicesLocator _externalServicesLocator;
        private readonly ICatalogExternalService _catalogExternalService;
        private readonly IDataDecodeExternalService _dataDecodeExternalService;
        private readonly IDecoderParsingFactory _decoderParsingFactory;
        private readonly IPromotionExternalService _promotionExternalService;

        public CatalogBc(IApplication application)
            : this(new ExternalServicesLocator(application))
        {
        }

        public CatalogBc(IExternalServicesLocator externalServicesLocator)
        {
            _externalServicesLocator = externalServicesLocator;
            _catalogExternalService = _externalServicesLocator.Locate<ICatalogExternalService>();
            _dataDecodeExternalService = _externalServicesLocator.Locate<IDataDecodeExternalService>();
            _decoderParsingFactory = _externalServicesLocator.Locate<IDecoderParsingFactory>();
            _promotionExternalService = _externalServicesLocator.Locate<IPromotionExternalService>();
        }

        public ItemSearchResponse GetItemsBySearchText(SearchRequest searchRequest)
        {
            return _catalogExternalService.GetItemsBySearchText(searchRequest);
        }

        public ItemLookupResponse GetItemLookup(string storeId, string itemId)
        {
            var response = new ItemLookupResponse();
            try
            {
                response = _catalogExternalService.GetItemLookup(storeId, itemId);
            }
            catch (ItemNotFoundException e)
            {
                try
                {
                    var decodedData = _dataDecodeExternalService.DecodeData(storeId, itemId, _decoderParsingFactory);
                    response.DecodedData = decodedData;
                    //if no decoding found for that item, it is actually not found.
                    if (!decodedData.Any())
                    {
                        throw;
                    }
                }
                catch (Exception e1)
                {
                    throw e;
                }
            }
            return response;
        }

        public ItemSearchResponse GetRelativeItems(string storeId, string itemId)
        {
            return _catalogExternalService.GetRelativeItems(storeId, itemId);
        }


        public ItemsLookupResponse GetItemsLookup(string storeId, string[] barcodes)
        {
            return _catalogExternalService.GetItemsLookup(storeId, barcodes);
        }

        public PromotionLookupResponse GetPromotionById(string storeId, string promotionId)
        {
            var response = new PromotionLookupResponse {Promotions = new List<Promotion>()};
            var promotion = _promotionExternalService.GetPromotionById(storeId, promotionId);
            if (promotion != null)
                response.Promotions.Add(promotion);

            return response;
        }

        public PromotionLookupResponse GetPromotionsByItemId(string storeId, string itemId)
        {
            return new PromotionLookupResponse
            {
                Promotions = _promotionExternalService.GetPromotionsByItemId(storeId, itemId)
            };
        }

        public SalesTransactionReponse PromotionApproval(string storeId, string transactionId, PromotionApprovalRequest promotionApprovalRequest)
        {
            return _promotionExternalService.PromotionApproval(storeId, transactionId, promotionApprovalRequest);
        }
    }
}