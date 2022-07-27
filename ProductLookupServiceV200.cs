using System;
using System.Linq;
using Common.Logging;
using Retalix.Contracts.Generated.ProductDomain.Product;
using Retalix.StoreServices.Model.Infrastructure.Exceptions;
using Retalix.StoreServices.Model.Infrastructure.Query;
using Retalix.StoreServices.Model.Infrastructure.Service;
using Retalix.StoreServices.Model.Infrastructure.StoreApplication;
using Retalix.StoreServices.Model.Organization.BusinessUnit;
using Retalix.StoreServices.Model.Product;
using Retalix.StoreServices.Model.Selling.ItemQuery.Events;

namespace Retalix.StoreServices.BusinessServices.Maintenance.Product.Lookup
{
    public class ProductLookupServiceV200 : IBusinessService
    {
        private readonly IProductDao _productDao;
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IBusinessUnitDao _businesssUnitDao;

        public ProductLookupServiceV200(IProductDao productDao, IBusinessUnitDao businesssUnitDao)
        {
            _productDao = productDao;
            _businesssUnitDao = businesssUnitDao;
        }

        public IDocumentResponse Execute(IDocumentRequest request)
        {
            var requestParser = new ProductLookupRequestParser(request, _businesssUnitDao, GlobalEnvironment.StoreApplication.Resolver);

            Validate(requestParser);

            var query = requestParser.GetQuery();
            var includeWith = requestParser.GetIncludeWith();
            var products = _productDao.FindByLookupCriteria(query, includeWith).ToList();
            var totalCounts = GetTotalCounts(requestParser.Request.Limit, query);

            if (products.Count == 1)
            {
                var relativeProducts = _productDao.FindByMerchandiseCategory(products.First());
                products.Clear();
                products.AddRange(relativeProducts);
            }

            var productLookupResponseBuilder = new ProductLookupResponseBuilder(products, totalCounts, requestParser);
            return productLookupResponseBuilder.Build();
        }

        private int? GetTotalCounts(ProductLookupRequestLimit limit, IQuery query)
        {
            if (limit == null || !limit.IncludeCount) return null;
            return _productDao.CountByLookupCriteria(query);
        }

        private static void Validate(ProductLookupRequestParser requestParser)
        {
            if (requestParser == null) throw new ArgumentNullException("requestParser");
            ValidateCriteria(requestParser.Request);
            ValidateSelection(requestParser.Request);
        }

        private static void ValidateSelection(ProductLookupRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Selection == null) throw new InvalidDataException("Selection is Missing, please provide a Selection");
        }

        private static void ValidateCriteria(ProductLookupRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (request.Criteria == null) return;

            var criteria = request.Criteria;

            var parameters = 0;
            if (criteria.Identifiers != null)
            {
                if (criteria.Identifiers.Code != null) 
                    parameters += criteria.Identifiers.Code.Length;

                if (criteria.Identifiers.CodeWithOrWithoutLeadingZero != null)
                    parameters += criteria.Identifiers.CodeWithOrWithoutLeadingZero.Length;
            }

            if (criteria.MainIds != null)
                parameters += criteria.MainIds.Id.Count();
            if (criteria.MainIdOrIdentifiers != null && !string.IsNullOrEmpty(criteria.MainIdOrIdentifiers.Value))
                parameters += 1;
            if (parameters > 2000)
                throw new BusinessException("Too many parameters in the product lookup", "InvalidProductLookup");

            RaiseItemSearchEvent(criteria);
        }

        private static void RaiseItemSearchEvent(CriteriaType criteria)
        {
            if (criteria != null && criteria.Description != null)
            {
                GlobalEnvironment.EventsDispatcher.Dispatch(new ItemSearchEvent()
                {
                    Description = criteria.Description.Value
                });
            }
        }

        public IDocumentResponse FormatErrorResponse(IDocumentRequest request, Exception exception)
        {
            Log.Error(exception);
            var requestId = GetRequestId(request);
            var builder = new ProductLookupErrorResponseBuilder(exception, requestId);
            return builder.Build();
        }

        private static string GetRequestId(IDocumentRequest request)
        {
            try
            {
                var requestObj = request.GetRequestObject<ProductLookupRequest>();
                return requestObj.Header.MessageId.Value;
            }
            catch (Exception exception)
            {
                Log.Warn("Failed to get RequestId for response", exception);
            }
            return null;
        }
    }
}