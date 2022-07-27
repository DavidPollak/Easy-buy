using System;
using System.Collections.Generic;
using Retalix.Contracts.Generated.Arts.PosLogV6.Source;
using Retalix.Contracts.Generated.Common;
using Retalix.Contracts.Generated.Item;
using Retalix.StoreServices.BusinessServices.FrontEnd;
using Retalix.StoreServices.BusinessServices.Maintenance.ItemPicker.Adaptor;
using Retalix.StoreServices.BusinessServices.Maintenance.ItemPicker.Builders;
using Retalix.StoreServices.Model.Infrastructure.Service;
using Retalix.StoreServices.Model.Infrastructure.StoreApplication;
using Retalix.StoreServices.Model.Product;
using Retalix.StoreServices.Model.Product.Hierarchy;

namespace Retalix.StoreServices.BusinessServices.Maintenance.ItemPicker
{
    /// <summary>
    /// Service Name: 
    ///     ItemGroupingByHierarchyLookupService
    /// Description of the Service:
    ///     ItemGroupingByHierarchyLookupService - Retrieves a item count groping by hierarchy according to ID of the hierarchy.    
    /// </summary>
    public class ItemGroupingByHierarchyLookupService : IBusinessService
    {
        private readonly IProductDao _productDao;

        private RtiRequestAdaptor _requestAdaptor;

        public ItemGroupingByHierarchyLookupService(IProductDao productDao)
        {
            _productDao = productDao;
        }

        public IDocumentResponse Execute(IDocumentRequest request)
        {
            _requestAdaptor = new RtiRequestAdaptor(request, GlobalEnvironment.StoreApplication.Resolver);

            var hierarchyType = _requestAdaptor.ItemHierarchyType;
            var includeEmptyCategories = _requestAdaptor.IncludeEmptyCategories;
            var query = _requestAdaptor.GetQuery();

            var hierarchyLevelNodeGroping = _productDao.CountOfProductsSatisfyingCriteriaPerCategoryTree(query, hierarchyType, includeEmptyCategories);


            return BuildResponse(hierarchyLevelNodeGroping, hierarchyLevelNodeGroping.Keys);
        }


        private IDocumentResponse BuildResponse(IDictionary<ICategory, int> hierarchyLevelNodeGroping, IEnumerable<ICategory> fullHierarchy)
        {
            var returnResponse =
                new ItemGroupingByHierarchyLookupResponseBuilder(_requestAdaptor.MessageId, hierarchyLevelNodeGroping,
                                                                 fullHierarchy)
                    .Build();

            return new DocumentResponse(returnResponse);
        }

        public IDocumentResponse FormatErrorResponse(IDocumentRequest request, Exception exception)
        {
            var errorCommonData =
                new RetalixBusinessErrorCommonData
                    {
                        Description = new DescriptionCommonData {Value = exception.Message,}
                    };
            var header =
                new RetalixCommonHeaderType
                    {
                        Response =
                            new RetalixResponseCommonData
                                {
                                    ResponseCode = "Rejected",
                                    BusinessError = new[] {errorCommonData},
                                }
                    };

            var response = new ItemGroupingByHierarchyLookupResponse
                               {
                                   ResponseHeader = header
                               };
            return new DocumentResponse(response);
        }

    }
}