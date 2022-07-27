using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Common.Logging;
using NHibernate;
using NHibernate.Criterion;
using Retalix.DPOS.SystemIntegrity;
using Retalix.StoreServices.Common.DMS.ETW;
using Retalix.StoreServices.Common.DMS.Heartbeat;
using Retalix.StoreServices.Connectivity.Item.Exceptions;
using Retalix.StoreServices.Connectivity.Item.Search;
using Retalix.StoreServices.Connectivity.Item.Search.Criteria;
using Retalix.StoreServices.Connectivity.Item.Search.Exceptions;
using Retalix.StoreServices.Connectivity.Product.Distribution;
using Retalix.StoreServices.Infrastructure.DataAccess;
using Retalix.StoreServices.Infrastructure.NHibernate.TextSearch;
using Retalix.StoreServices.Model.Infrastructure.BusinessActivity;
using Retalix.StoreServices.Model.Infrastructure.DataAccess;
using Retalix.StoreServices.Model.Infrastructure.Entity;
using Retalix.StoreServices.Model.Infrastructure.Exceptions;
using Retalix.StoreServices.Model.Infrastructure.Legacy.Bulk;
using Retalix.StoreServices.Model.Infrastructure.Query;
using Retalix.StoreServices.Model.Infrastructure.Queue;
using Retalix.StoreServices.Model.Infrastructure.RetentionPolicy;
using Retalix.StoreServices.Model.Infrastructure.StoreApplication;
using Retalix.StoreServices.Model.Product;
using Retalix.StoreServices.Model.Product.Hierarchy;
using Retalix.StoreServices.Model.Product.Lookup;
using Retalix.StoreServices.Model.Product.ProductAttribute;
using Retalix.StoreServices.Connectivity.Product.ProductClassification;
using ICriterion = Retalix.StoreServices.Model.Infrastructure.Query.ICriterion;
using IQuery = NHibernate.IQuery;

namespace Retalix.StoreServices.Connectivity.Item
{
    public class ProductDao : IProductDao, IProductMovableDao, IEntityDao
    {
        private readonly ISessionProvider<ISession> _sessionProvider;
        private readonly IResolver _serviceResolver;
        private readonly IProductFactory _productFactory;
        private readonly IBusinessActivityController _businessActivityController;
        private readonly IProductMovableConvertor _productMovableConvertor;
        private ProductBulkLoader _productBulkLoader;
        private ProductAttributesBulkLoader _productAttributesBulkLoader;
        private IProductEventSource _productEventSource;
        private IProductRemotableDao _productRemotableDao;

        protected ProductBulkLoader ProductBulkLoader
        {
            get
            {
                if (_productBulkLoader != null) return _productBulkLoader;

                var commandExecuter = _serviceResolver.Resolve<IDbCommandExecuter>();
                _productBulkLoader = new ProductBulkLoader(commandExecuter, _productFactory, _serviceResolver, _sessionProvider);

                return _productBulkLoader;
            }
        }

        protected ProductAttributesBulkLoader ProductAttributesBulkLoader
        {
            get
            {
                if (_productAttributesBulkLoader != null) return _productAttributesBulkLoader;

                var commandExecuter = _serviceResolver.Resolve<IDbCommandExecuter>();
                _productAttributesBulkLoader = new ProductAttributesBulkLoader(commandExecuter);

                return _productAttributesBulkLoader;
            }
        }

        protected ILog Logger
        {
            get { return LogManager.GetLogger(typeof(ProductDao)); }
        }

        protected virtual ISession Session
        {
            get { return _sessionProvider.Session; }
        }

        private IProductEventSource ProductEventSource
        {
            get { return _productEventSource ?? (_productEventSource = _serviceResolver.Resolve<IProductEventSource>()); }
        }

        private IProductRemotableDao ProductRemotableDao
        {
            get { return _productRemotableDao ?? (_productRemotableDao = _serviceResolver.Resolve<IProductRemotableDao>()); }
        }

        public ProductDao(ISessionProvider<ISession> sessionProvider, IResolver serviceResolver, IProductFactory productFactory, IBusinessActivityController businessActivityController, IProductMovableConvertor productMovableConvertor)
        {
            _serviceResolver = serviceResolver;
            _productFactory = productFactory;
            _sessionProvider = sessionProvider;
            _businessActivityController = businessActivityController;
            _productMovableConvertor = productMovableConvertor;
            new ProductByIdentifiersOrDescriptions(_serviceResolver, _sessionProvider);
        }

        public IProduct Get(string entityKey)
        {
            var remoteOrLocalProduct = ProductRemotableDao.Get(entityKey);
            return ProductInSessionOrProxy(remoteOrLocalProduct);
        }

        public virtual IEnumerable<IProduct> Get(IEnumerable<string> entityKeys)
        {
            var entityKeysToFind = entityKeys.ToList();
            var remoteOrLocalProducts = new List<IProduct>();

            while (entityKeysToFind.Any())
            {
                var products = ProductRemotableDao.Get(entityKeysToFind);
                if (!products.Any())
                    break;
                remoteOrLocalProducts.AddRange(products.Select(ProductInSessionOrProxy));

                foreach (var product in products)
                    entityKeysToFind.Remove(product.EntityKey);
            }

            return remoteOrLocalProducts;
        }

        public IEnumerable<IProduct> Find<TRole>(IEnumerable<IQueryCriterion<IProduct>> criteria = null, IEnumerable<IQuerySpecification<IProduct>> specifications = null)
        {
            var remoteOrLocalProducts = ProductRemotableDao.Find<TRole>(criteria, specifications);
            return remoteOrLocalProducts.Select(ProductInSessionOrProxy).ToArray();
        }

        public IProduct GetByIdentifier(Identifier productIdentifier)
        {
            var remoteOrLocalProduct = ProductRemotableDao.GetByIdentifier(productIdentifier);
            return ProductInSessionOrProxy(remoteOrLocalProduct);
        }

        private IProduct ProductInSessionOrProxy(IProduct remoteOrLocalProduct)
        {
            if (remoteOrLocalProduct == null)
                return null;

            var entityKey = remoteOrLocalProduct.EntityKey;
            var localProduct = Session.Get<IProduct>(entityKey);

            if (localProduct != null)
                return localProduct;

            var remoteProduct = Session.Load<IProduct>(entityKey);
            NHibernateUtil.Initialize(remoteProduct);

            return remoteProduct;
        }

        public void Save(IProduct product)
        {
            Save(new[] { product });
        }

        public void Save(IEnumerable<IProduct> products)
        {
            _businessActivityController.TriggerActivity(BusinessActivities.Product.ConfigureGeneral);

            foreach (var product in products)
            {
                foreach (var identifier in product.Identifiers) {
                    identifier.LastUpdated = DateTime.SpecifyKind(DateTimeService.Instance.Now, DateTimeKind.Unspecified);
                }

                ValidateIdentifersUnique(product);
                Session.SaveOrUpdate(product);
            }

            var prodctDtos = products.Where(product => !product.EntityKey.StartsWith("DEP_")).Select(product => _productMovableConvertor.Convert(product)).ToList();

            GlobalEnvironment.EventsDispatcher.Dispatch(new QueueMessageWriterEvent<ProductMaintenanceDTO>(prodctDtos));

            Session.Flush();
        }

        public IDictionary<ICategory, int> CountOfProductsSatisfyingCriteriaPerCategoryTree(ProductSearchCriteria productSearchCriteria, string classificationType, bool includeEmptyCategories = false)
        {
            if (productSearchCriteria == null)
                throw new EmptyCriteriaException();

            var groupingResult = RetreiveCategoriesWithProductCount(productSearchCriteria, classificationType);

            var categories = includeEmptyCategories ? GetAllCategories(classificationType) : GetPartialCategories(classificationType, groupingResult);

            var handler = new ItemGroupingHandler(categories, groupingResult);
            var allHierarchyWithConsumableCount = handler.GetCountPerCategory();

            return allHierarchyWithConsumableCount;
        }

        public IDictionary<ICategory, int> CountOfProductsSatisfyingCriteriaPerCategoryTree(Model.Infrastructure.Query.IQuery productQuery, string classificationType, bool includeEmptyCategories = false)
        {
            if (productQuery == null)
                throw new EmptyCriteriaException();

            var builder = _serviceResolver.Resolve<IQueryBuilder>();

            var productCriteria = classificationType == "Merchandise" ? CreateProductCriteria() : CreateProductClassificationCriteria();

            builder.Execute(Session, productCriteria, productQuery);

            var groupingResult = RetreiveCategoriesWithProductCount(productCriteria);

            var categories = includeEmptyCategories ? GetAllCategories(classificationType) : GetPartialCategories(classificationType, groupingResult);

            var handler = new ItemGroupingHandler(categories, groupingResult);
            var allHierarchyWithConsumableCount = handler.GetCountPerCategory();

            return allHierarchyWithConsumableCount;
        }

        private IEnumerable<ICategory> GetPartialCategories(string classificationType, ProductCountPerCategoryResult groupingResult)
        {
            var result = groupingResult.ProductPerCategory;
            if (!result.Keys.Any())
                return new List<ICategory>();
            var hierarchyDao = _serviceResolver.Resolve<ICategoryDao>();
            var categories = (classificationType == "Merchandise")
                                 ? hierarchyDao.GetMerchandiseCategories(result.Keys).OfType<ICategory>()
                                 : hierarchyDao.GetCustomCategories(classificationType, result.Keys);

            return categories;
        }

        private IEnumerable<ICategory> GetAllCategories(string classificationType)
        {
            var hierarchyDao = _serviceResolver.Resolve<ICategoryDao>();
            var categories = (classificationType == "Merchandise")
                                 ? hierarchyDao.GetMerchandiseCategories().OfType<ICategory>()
                                 : hierarchyDao.GetCustomCategories(classificationType);

            if (!categories.Any())
                throw new ArgumentException(string.Format("ClassificationType type {0} doesn't exist", classificationType));

            return categories;
        }

        public int CountOfProductsSatisfyingCriteria(ProductSearchCriteria productSearchCriteria)
        {
            var consumableCriteria = GetCommonCriterias(productSearchCriteria).GetConsumableCriteria();
            return consumableCriteria.SetProjection(Projections.Count("EntityKey")).UniqueResult<int>();
        }

        public IEnumerable<IProduct> FindBySearchCriteria(ProductSearchCriteria productSearchCriteria)
        {
            var sortingCriterias = productSearchCriteria.SortingCriterias;
            if (sortingCriterias.Count > 1)
                throw new UnsupportedCombinedSortingException();
            var sortingCriteria = sortingCriterias.FirstOrDefault() ?? new EntityKeySortingCriteria();

            var sortingCriteriaBuilderProvider = new SortingCriteriaBuilderProvider(_serviceResolver);
            var sortingCriteriaBuilder = sortingCriteriaBuilderProvider.FindFor(sortingCriteria);
            var productCriteria = GetCommonCriterias(productSearchCriteria).GetConsumableCriteria();
            productCriteria = sortingCriteriaBuilder.AddSorting(Session, productCriteria, sortingCriteria);

            new LookupPagingStrategy(productSearchCriteria.PagingCriteria).ApplyTo(productCriteria);

            var products = GetResultOrFirstProjection(productCriteria);
            new ProductEagerLoadingHelper(Session, products, productSearchCriteria.EagerLoading).EagerLoad();

            return products;
        }

        public IEnumerable<IProduct> FindByMerchandiseCategory(IProduct product)
        {
            IList<IProduct> products = Session.GetNamedQuery("GetProductByCategory")
                .SetParameter("category", product.MerchandiseCategory.Id)
                .List<IProduct>();


            return products;
        }

        public IEnumerable<IProduct> FindByLookupCriteria(Model.Infrastructure.Query.IQuery productQuery, ProductEagerLoadingCriteria includeWith)
        {
            if (productQuery == null)
                throw new EmptyCriteriaException();

            var builder = _serviceResolver.Resolve<IQueryBuilder>();

            ICriteria productCriteria = null;

            var externalSortFound = false;
            if (productQuery.Sort != null)
            {
                foreach (var sort in productQuery.Sort.Where(sort => sort.IsExternalSort))
                {
                    if (externalSortFound)
                        throw new InvalidDataException("Can't have more that one external to the product sort section");
                    productCriteria = CreateExternalCriteria(sort.ExternalType, sort.ExternalAlias);
                    externalSortFound = true;
                }
            }

            if (!externalSortFound)
                productCriteria = CreateProductCriteria();

            if (ProductByIdentifiersOrDescriptions.IsMainIdOrIdentifiersOrDescriptionsCriteria(productQuery, includeWith))
                return ProductByIdentifiersOrDescriptions.GetProducts(productQuery, includeWith);

            ((List<ICriterion>)productQuery.Criteria).Add(new AddNoDepartmentCriterion());

            builder.Execute(Session, productCriteria, productQuery);
            IEnumerable<IProduct> products;
            try
            {
                products = GetResultOrFirstProjection(productCriteria);
                products = ProductByStrippingLeadingZeroIdentifiers.Find(products.ToList(), productQuery, includeWith, this);
            }
            catch (SqlException sqlException)
            {
                var isWordBreakerTimeoutException = sqlException.Number == 30053;
                if (!isWordBreakerTimeoutException)
                    throw;

                Logger.Error("ProductDao caught exception for SQL Server Full Text Search.", sqlException);
                throw new BusinessException("The system returned a large number of results. Please enter additional characters to narrow your search.", "TooManyResults", sqlException);
            }

            if (includeWith != null)
                new ProductEagerLoadingHelper(Session, products, includeWith)
                    .EagerLoad();

            return products;
        }

        public int CountByLookupCriteria(Model.Infrastructure.Query.IQuery productQuery)
        {
            if (productQuery == null)
                throw new EmptyCriteriaException();

            var query = ProductByIdentifiersOrDescriptions.IsMainIdOrIdentifiersOrDescriptionsCriteria(productQuery) ?
                BuildQueryByIsMainIdOrIdentifiersOrDescriptionsCriteria(productQuery) :
                BuildQueryByMainIdProductCriterion(productQuery);

            if (query == null && ProductByIdentifiersOrDescriptions.IsAddNoDepartmentCriterion(productQuery))
                query = BuildQueryByAddNoDepartmentCriterion(productQuery);

            if (query == null)
                return 0;

            var count = query.UniqueResult<int>();

            return count;
        }

        private IQuery BuildQueryByAddNoDepartmentCriterion(Model.Infrastructure.Query.IQuery productQuery)
        {
            const string query = @"SELECT COUNT(1) FROM CAT_Product p
                                    LEFT OUTER JOIN CAT_Department d
                                    ON p.Product_Id = d.Product_Id
                                    WHERE Department_Id IS NULL";

            return Session.CreateSQLQuery(query);
        }

        private IQuery BuildQueryByMainIdProductCriterion(Model.Infrastructure.Query.IQuery productQuery)
        {
            var mainIdCriterion = productQuery.Criteria.FirstOrDefault(x => x is MainIdProductCriterion);
            if (mainIdCriterion is MainIdProductCriterion)
            {
                var mainIdCriteria = mainIdCriterion as MainIdProductCriterion;
                return GetSearchQueryByMainId(mainIdCriteria);
            }

                

            var productDescriptionCriterion = productQuery.Criteria.FirstOrDefault(x => x is DescriptionProductCriterion);
            var productDescriptionCriteria = productDescriptionCriterion as DescriptionProductCriterion;

            var mainIdOrIdentifiersCriterion = productQuery.Criteria.FirstOrDefault(x => x is MainIdOrIdentifiersCriterion);
            var mainIdOrIdentifiersCriteria = mainIdOrIdentifiersCriterion as MainIdOrIdentifiersCriterion;

            var storeRangeCriterion = productQuery.Criteria.FirstOrDefault(x => x is StoreRangeProductCriterion);
            var storeRangeCriteria = storeRangeCriterion as StoreRangeProductCriterion;

            var hierarchyCriterion = productQuery.Criteria.FirstOrDefault(x => x is HierarchyProductCriterion);
            var hierarchyCriteria = hierarchyCriterion as HierarchyProductCriterion;

            var itemCodeCondition = string.Empty;
            var itemCode = string.Empty;

            var getByItemCodeAndDescription = productDescriptionCriteria != null && mainIdOrIdentifiersCriteria != null;
            if (mainIdOrIdentifiersCriteria != null && !string.IsNullOrEmpty(mainIdOrIdentifiersCriteria.Code))
                itemCodeCondition = GetSubQuerySearchItemCodeCondition(mainIdOrIdentifiersCriteria, out itemCode, getByItemCodeAndDescription);

            if (productDescriptionCriteria == null && hierarchyCriteria == null)
                return GetQueryByMainIdOrIdentifiers(mainIdOrIdentifiersCriteria, itemCode);
            if (productDescriptionCriteria == null)
            {
                var queryByHierarchy = GetQueryByMerchandizeHierarchy(itemCodeCondition, hierarchyCriteria, mainIdOrIdentifiersCriteria, itemCode);

                if (queryByHierarchy != null) return queryByHierarchy;
            }

            var description = productDescriptionCriteria!= null ? productDescriptionCriteria.Descriptions.FirstOrDefault(): null;
            if (description == null)
                return null;

            description.Value = WrapSpecialCharacters(description.Value);

            var query = @"SELECT COUNT(DISTINCT (pd.Product_Id)) FROM CAT_ProductDescription pd 
                        INNER JOIN CAT_Product p ON pd.Product_Id = p.Product_Id
                        WHERE pd.Value LIKE :description";

            var culture = description.Culture != "*" ? description.Culture : string.Empty;
            var cultureFilter = culture != string.Empty ? " AND pd.Culture = :culture" : string.Empty;

            var param = string.Format("{0}%", description.Value);

            if (string.IsNullOrEmpty(cultureFilter) && hierarchyCriteria == null)
                return GetQueryByDescriptionAndItemCode(itemCodeCondition, mainIdOrIdentifiersCriteria, query, param, itemCode, storeRangeCriteria);
            if (hierarchyCriteria != null)
                return GetQueryByDescriptionAndHierarchyName(itemCodeCondition, hierarchyCriteria, param, itemCode);

            query += cultureFilter;
            query += itemCodeCondition;

            var prebuiltQuery = string.IsNullOrEmpty(itemCode) ?
                Session.CreateSQLQuery(query).SetString("description", param).SetString("culture", culture) :
                Session.CreateSQLQuery(query).SetString("description", param).SetString("culture", culture).SetString("itemCode", itemCode);

            return prebuiltQuery;
        }
        private static bool IsMerchandiseRoot(HierarchyProductCriterion hierarchyCriteria)
        {
            var categories = hierarchyCriteria != null && hierarchyCriteria.CategoriesPerType != null ? hierarchyCriteria.CategoriesPerType.Values.FirstOrDefault() : null;

            return categories != null && categories.Any(IsMerchandise);
        }

        private static bool IsMerchandise(ICategory category)
        {
            var hierarchyPath = category.GetHierarchyPath();
            return !string.IsNullOrEmpty(hierarchyPath) && hierarchyPath.Equals("0@");
        }
        private IQuery GetQueryByMerchandizeHierarchy(string itemCodeCondition, HierarchyProductCriterion hierarchyCriteria, MainIdOrIdentifiersCriterion mainIdOrIdentifiersCriteria, string itemCode)
        {
            var hierarchyName = GetHierarchyName(hierarchyCriteria);
            if (hierarchyName == null) return null;

            const string hierarchyCondition = " AND hd.Value = :hierarchyName ";

            var query = "SELECT COUNT(DISTINCT p.Product_Id) FROM CAT_ProductDescription pd " +
                        "INNER JOIN CAT_Product p ON pd.Product_Id = p.Product_Id " +
                        "INNER JOIN CAT_Hierarchy ch on ch.Ik = p.MerchandiseCategoryFk " +
                        "INNER JOIN CAT_HierarchyDescription hd ON p.MerchandiseCategoryFk = hd.HierarchyFk ";

            if (string.IsNullOrEmpty(itemCode) && !IsMerchandiseRoot(hierarchyCriteria))
            {
                query += hierarchyCondition;
                return Session.CreateSQLQuery(query).SetString("hierarchyName", hierarchyName);
            }

            if (mainIdOrIdentifiersCriteria.Mode.ToString().Equals("ANYWHERE"))
                itemCode = string.Format("%{0}%", itemCode);

            query += " AND " + itemCodeCondition;

            if (IsMerchandiseRoot(hierarchyCriteria))
                return Session.CreateSQLQuery(query).SetString("itemCode", itemCode);

            query += hierarchyCondition;

            return Session.CreateSQLQuery(query)
                .SetString("hierarchyName", hierarchyName)
                .SetString("itemCode", itemCode);

        }

        private static string GetHierarchyName(HierarchyProductCriterion hierarchyCriteria)
        {
            return hierarchyCriteria.CategoriesPerType.FirstOrDefault().Value.FirstOrDefault()!=null? hierarchyCriteria.CategoriesPerType.FirstOrDefault().Value.FirstOrDefault().Descriptions.FirstOrDefault().Value: null;
        }

        private IQuery GetQueryByDescriptionAndHierarchyName(string itemCodeCondition, HierarchyProductCriterion hierarchyCriteria, string description, string itemCode)
        {
            var hierarchyName = GetHierarchyName(hierarchyCriteria);
            const string hierarchyCondition = " AND hd.Value = :hierarchyName ";

            var query = "SELECT DISTINCT COUNT(DISTINCT p.Product_Id) FROM CAT_ProductDescription pd " +
                        "INNER JOIN CAT_Product p ON pd.Product_Id = p.Product_Id " +
                        "INNER JOIN CAT_Hierarchy ch on ch.Ik = p.MerchandiseCategoryFk " +
                        "INNER JOIN CAT_HierarchyDescription hd ON p.MerchandiseCategoryFk = hd.HierarchyFk " +
                        "WHERE pd.Value LIKE :description  ";

            if (string.IsNullOrEmpty(itemCodeCondition))
            {
                if (IsMerchandiseRoot(hierarchyCriteria))
                    return Session.CreateSQLQuery(query).SetString("description", description);

                query += hierarchyCondition;
                return Session.CreateSQLQuery(query).SetString("description", description).SetString("hierarchyName", hierarchyName);
            }

            if (!string.IsNullOrEmpty(itemCodeCondition))
                itemCode = string.Format("%{0}%", itemCode);

            query += itemCodeCondition;

            if (IsMerchandiseRoot(hierarchyCriteria))
                return Session.CreateSQLQuery(query).SetString("description", description).SetString("itemCode", itemCode);

            query += hierarchyCondition;
            return Session.CreateSQLQuery(query)
                .SetString("description", description)
                .SetString("hierarchyName", hierarchyName)
                .SetString("itemCode", itemCode);
        }


        private IQuery GetQueryByDescriptionAndItemCode(string itemCodeCondition, MainIdOrIdentifiersCriterion mainIdOrIdentifiersCriteria,
            string query, string description, string itemCode, StoreRangeProductCriterion storeRangeCriteria)
        {
            if (storeRangeCriteria == null)
            {
                if (string.IsNullOrEmpty(itemCodeCondition) || mainIdOrIdentifiersCriteria == null)
                    return Session.CreateSQLQuery(query).SetString("description", description);

                if (mainIdOrIdentifiersCriteria.Mode.ToString().Equals("ANYWHERE"))
                    itemCode = string.Format("%{0}%", itemCode);

                query += itemCodeCondition;
                return Session.CreateSQLQuery(query).SetString("description", description).SetString("itemCode", itemCode);
            }

            var businessUnitId = storeRangeCriteria.BusinessUnit;
            var isInclude = storeRangeCriteria.Value.ToString();

            query = @"SELECT COUNT(DISTINCT (pd.Product_Id)) FROM CAT_ProductDescription pd 
                        INNER JOIN CAT_Product p ON pd.Product_Id = p.Product_Id 
                        INNER JOIN CAT_StoreRangeProduct srp ON srp.Product_Id = p.Product_Id 
                        WHERE pd.Value LIKE :description 
                        AND srp.IsInclude = :isInclude
                        AND srp.BusinessUnit_Id IN (SELECT BU_ID FROM [ColdStart].[udf_GetBU_Hierarchy](:businessUnitId))";

            return Session.CreateSQLQuery(query)
                .SetString("description", description)
                .SetString("isInclude", isInclude)
                .SetString("businessUnitId", businessUnitId);
        }

        private IQuery GetQueryByMainIdOrIdentifiers(MainIdOrIdentifiersCriterion mainIdOrIdentifiersCriteria, string itemCode)
        {
            var query = @"SELECT COUNT(DISTINCT (pd.Product_Id)) FROM CAT_ProductDescription pd 
                            INNER JOIN CAT_ProductIdentifier p ON pd.Product_Id = p.Product_Id";

            if (mainIdOrIdentifiersCriteria == null || string.IsNullOrEmpty(mainIdOrIdentifiersCriteria.Code)) return null;

            if (!mainIdOrIdentifiersCriteria.Mode.ToString().Equals("ANYWHERE"))
            {
                query += @" WHERE p.Product_Id = :itemCode";
                return Session.CreateSQLQuery(query).SetString("itemCode", itemCode);
            }

            itemCode = string.Format("%{0}%", itemCode);
            query += " WHERE p.Code LIKE :itemCode";

            return Session.CreateSQLQuery(query).SetString("itemCode", itemCode);
        }

        private static string GetSubQuerySearchItemCodeCondition(MainIdOrIdentifiersCriterion mainIdOrIdentifiersCriteria, out string itemCode, bool andCondition)
        {
            var itemCodeCondition = mainIdOrIdentifiersCriteria.Mode.ToString().Equals("ANYWHERE") ?
                " p.Product_Id LIKE :itemCode" :
                " p.Product_Id = :itemCode";

            if (andCondition)
                itemCodeCondition = itemCodeCondition.Insert(0, " AND ");

            itemCode = WrapSpecialCharacters(mainIdOrIdentifiersCriteria.Code);
            return itemCodeCondition;
        }

        private IQuery GetSearchQueryByMainId(MainIdProductCriterion mainIdCriteria)
        {
            var searchParameters = mainIdCriteria.SearchParamerters.FirstOrDefault();

            if (searchParameters == null)
                return null;

            const string query = "SELECT COUNT(1) FROM CAT_Product WHERE Product_Id LIKE :mainId";
            var param = string.Format("{0}%", searchParameters.Value);
            return Session.CreateSQLQuery(query).SetString("mainId", param);
        }

        private static string WrapSpecialCharacters(string description)
        {
            if (!description.Contains("%") && !description.Contains("_")) return description;

            description = description.Replace("_", "[_]");
            description = description.Replace("%", "[%]");

            return description;
        }

        private IQuery BuildQueryByIsMainIdOrIdentifiersOrDescriptionsCriteria(Model.Infrastructure.Query.IQuery productQuery)
        {
            var product = productQuery.Criteria.FirstOrDefault();

            var productCriterion = product as MainIdOrIdentifiersOrDescriptionsCriterion;

            if (productCriterion == null)
                return null;

            var query =
                    @"SELECT count(p.Product_Id) FROM CAT_Product p LEFT  
                    OUTER JOIN CAT_ProductCalorie c ON p.Product_Id = c.Product_Id 
                    LEFT OUTER JOIN (SELECT DISTINCT Product_Id 
                    FROM CAT_ProductIdentifier WHERE Code like :productCriterion) i ON p.Product_Id = i.Product_Id 
                    INNER JOIN (SELECT DISTINCT Product_Id 
                    FROM CAT_ProductDescription WHERE Value like :productCriterion) d ON p.Product_Id = d.Product_Id 
                    WHERE p.Product_Id = p.Product_Id or p.Product_Id like :productCriterion";

            WrapSpecialCharacters(productCriterion.Text);

            var param = string.Format("%{0}%", productCriterion.Text);

            return Session.CreateSQLQuery(query)
                .SetString("productCriterion", param);

        }

        [Obsolete("This method is obsolete since version 10.5", false)]
        public IEnumerable<IProduct> FindBySearchCriteria(ProductSearchCriteria productSearchCriteria, int pageNumber = 0, int pageSize = 300)
        {
            productSearchCriteria.PagingCriteria = new PagingCriteria
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return FindBySearchCriteria(productSearchCriteria);
        }

        public IEnumerable<IProduct> FetchAllOrdered(string[] businessUnitIds)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("Consumable");
                return ProductBulkLoader.FetchAllOrdered(businessUnitIds);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("Consumable");
            }
        }

        public IEnumerable<IProductAttribute> FetchAllAttributes(string[] businessUnitIds)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("ConsumableAttribute");
                return ProductAttributesBulkLoader.FetchAllOrdered(businessUnitIds);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("ConsumableAttribute");
            }
        }

        public IEnumerable<IProductAttribute> FetchAllAttributesFromPosition(string[] businessUnitIds, string lastProductEntityKey)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("ConsumableAttribute");
                return ProductAttributesBulkLoader.FetchAllOrdered(businessUnitIds, lastProductEntityKey);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("ConsumableAttribute");
            }
        }

        public IEnumerable<IProduct> FetchAllOrderedFromPosition(string[] businessUnitIds, string lastProductEntityKey)
        {
            return ProductBulkLoader.FetchAllOrdered(businessUnitIds, lastProductEntityKey);
        }

        public IEnumerable<IProduct> FetchAllOrderedNoAttributes(string[] businessUnitIds)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("Consumable");
                return ProductBulkLoader.FetchAllOrderedNoAttributes(businessUnitIds);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("Consumable");
            }
        }

        public IEnumerable<IProduct> FetchAllOrderedFromPositionNoAttributes(string[] businessUnitIds, string lastProductEntityKey)
        {
            return ProductBulkLoader.FetchAllOrderedNoAttributes(businessUnitIds, lastProductEntityKey);
        }

        public IEnumerable<IProduct> FetchAllOrderedBasic(string[] businessUnitIds)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("Consumable");
                return ProductBulkLoader.FetchAllOrderedBasic(businessUnitIds);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("Consumable");
            }
        }

        public IEnumerable<IProduct> FetchAllOrderedBasicFromPosition(string[] businessUnitIds, string lastProductEntityKey)
        {
            try
            {
                // ETW Trace
                ProductEventSource.FetchAllStarted("Consumable");
                return ProductBulkLoader.FetchAllOrderedBasic(businessUnitIds, lastProductEntityKey);
            }
            finally
            {
                // ETW Trace
                ProductEventSource.FetchAllCompleted("Consumable");
            }
        }

        public IEnumerable<IProduct> FetchSelectedProducts(string[] productIds)
        {
            return ProductBulkLoader.FetchSelectedProducts(productIds);
        }

        private IProduct GetByIdentifierOrNull(Identifier productIdentifier)
        {
            var query = Session.GetNamedQuery("GetProductByIdentifier");
            var product = query
                .SetCacheable(true)
                .SetParameter("code", productIdentifier.Code)
                .SetParameter("type", productIdentifier.Type)
                .UniqueResult<IProduct>();
            return product;
        }

        private static ProductCountPerCategoryResult RetreiveCategoriesWithProductCount(ICriteria criteria)
        {
            var result = new ProductCountPerCategoryResult();
            criteria
                 .SetResultTransformer(new CategoryGroupingResultTransformer(result))
                 .List();
            return result;
        }

        private ProductCountPerCategoryResult RetreiveCategoriesWithProductCount(ProductSearchCriteria productSearchCriteria, string classificationType)
        {
            var result = new ProductCountPerCategoryResult();

            GetCommonCriterias(productSearchCriteria)
                .CreateHierarchiesGroupingCriteria(classificationType)
                .SetResultTransformer(new CategoryGroupingResultTransformer(result))
                .List();

            return result;
        }

        private ConsumableCriteriaBuilder GetCommonCriterias(ProductSearchCriteria productSearchCriteria)
        {
            var searchCriterionProvider = GlobalEnvironment.StoreApplication.Resolver.Resolve<ISearchCriterionProvider>();

            var mainProductCriteriaBuilder = new ConsumableCriteriaBuilder(Session, searchCriterionProvider);
            ApplyConjunctionCriteria(productSearchCriteria, mainProductCriteriaBuilder);

            if (productSearchCriteria.Disjunction.Any())
            {
                var disjunctionCriterias = new List<DetachedCriteria>();
                foreach (var conjunctionCriteria in productSearchCriteria.Disjunction)
                {
                    var productCriteriaBuilder = new ConsumableCriteriaBuilder(Session,
                                                                               searchCriterionProvider);
                    ApplyConjunctionCriteria(conjunctionCriteria, productCriteriaBuilder);
                    disjunctionCriterias.Add(productCriteriaBuilder.GetConsumableCriteriaProjection());
                }
                mainProductCriteriaBuilder.AddDisjunctions(disjunctionCriterias);
            }

            mainProductCriteriaBuilder.AddNoDepartmentCriteria();
            return mainProductCriteriaBuilder;
        }

        private static void ApplyConjunctionCriteria(ProductConjunctionCriteria conjunctionCriteria, ConsumableCriteriaBuilder consumableCriteriaBuilder)
        {
            consumableCriteriaBuilder
                .AddDescriptionCriteria(conjunctionCriteria.DescriptionCriterias)
                .AddIdentifierCriteria(conjunctionCriteria.IdentifierCriterias)
                .AddIdentifiersCriteria(conjunctionCriteria.IdentifiersCriterias)
                .AddIdentifierOrCodeCriteria(conjunctionCriteria.IdentifierOrCodeCriterias)
                .AddDescriptionOrCodeCriteria(conjunctionCriteria.DescriptionOrCodeCriterias)
                .AddDescriptionOrIdentifierOrCodeCriteria(conjunctionCriteria.DescriptionOrIdentifierOrCodeCriterias)
                .AddMainIdCriteria(conjunctionCriteria.MainIdCriterias)
                .AddHierarchiesDescriptionCriteria(conjunctionCriteria.HierarchyDescriptionCriterias)
                .AddHierarchiesCriteria(conjunctionCriteria.HierarchyCriterias)
                .AddStoreRangeCriteria(conjunctionCriteria.StoreRangeCriteria);
        }

        private IEnumerable<IProduct> GetResultOrFirstProjection(ICriteria productCriteria)
        {
            var products = new List<IProduct>();
            var results = productCriteria.List();
            foreach (var result in results)
            {
                var product = result as IProduct;
                var productId = result as string;

                if (productId != null)
                    product = (IProduct)Session.Load("Retalix.StoreServices.BusinessComponents.Product.Legacy.Item.Consumable", productId);

                if (product == null)
                    product = (IProduct)(((object[])result)[0]);

                products.Add(product);
            }

            return products;
        }

        private void ValidateIdentifersUnique(IProduct product)
        {
            foreach (var identifier in product.Identifiers)
            {
                var productByIdentifier = GetByIdentifierOrNull(identifier);
                if (productByIdentifier != null)
                {
                    if (productByIdentifier.EntityKey != product.EntityKey)
                    {
                        throw new ProductIdentifierNotUniqueException(
                            productByIdentifier.EntityKey, identifier.Code,
                            BusinessExceptionErrorCodes.ProductIdentifierNotUniqueException);
                    }
                }
            }
        }

        public int Delete(IEntityDeleteCriteria criteria)
        {
            var isFilterEnabled = ProductFilteringFlagProvider.IsProductFilteringEnabled();
            if (!isFilterEnabled)
                return 0;

            var serverInfoRepository = _serviceResolver.Resolve<IServerInfoRepository>();
            var serverInfo = serverInfoRepository.GetRootServer();

            if (serverInfo == null)
                return 0;

            var businessUnitId = serverInfo.LogicalNodeId;
            var daysToKeepDate = DateTimeService.Instance.Now.AddDays(-criteria.DaysToKeep).ToString("MM/dd/yy H:mm:ss");

            var pageSize = criteria.PageSize;


            Session.CreateSQLQuery("create table #ratetable(Product_Id varchar(40))").ExecuteUpdate();
            const string query = @"insert into #ratetable SELECT top({0}) Product_Id FROM
                                    (
	                                    SELECT SRP.Product_Id, SRP.IsInclude
		                                     , SRP.StartDate, MAX(SRP.StartDate) OVER(PARTITION BY SRP.BusinessUnit_Id, SRP.Product_Id) MAX_StartDate
		                                     , ST.[Related_BU_Level],  MAX(ST.[Related_BU_Level]) OVER(PARTITION BY SRP.Product_Id) MAX_Level
	                                    FROM dbo.CAT_StoreRangeProduct SRP
		                                    INNER JOIN BU_Hierarchy ST ON SRP.BusinessUnit_Id = ST.Related_BU_Id
	                                    WHERE ST.BU_ID = '{1}' 
		                                    AND SRP.StartDate <= '{2}' 
                                    ) T
                                    WHERE	[Related_BU_Level] = MAX_Level 
	                                    AND StartDate = MAX_StartDate
	                                    AND IsInclude = 'false'
	                                    and Product_Id not in
	                                    (
			                                SELECT Product_Id
			                                FROM
			                                (
				                                   SELECT SRP.Product_Id, SRP.IsInclude
					                                 , ST.[Related_BU_Level],  MAX(ST.[Related_BU_Level]) OVER(PARTITION BY SRP.Product_Id) MAX_Level
				                                FROM dbo.CAT_StoreRangeProduct SRP
					                                INNER JOIN BU_Hierarchy ST ON SRP.BusinessUnit_Id = ST.Related_BU_Id
				                                WHERE ST.BU_ID = '{1}' 
					                                AND SRP.StartDate >= '{2}' 
			                                ) T
			                                WHERE	[Related_BU_Level] = MAX_Level 
				                                AND IsInclude = 'true'
	                                     )";
            var queryString = string.Format(query, pageSize, businessUnitId, daysToKeepDate);
            Session.CreateSQLQuery(queryString).ExecuteUpdate();

            var deletedCount = Session.CreateSQLQuery("DELETE FROM Cat_Product FROM Cat_Product INNER JOIN #ratetable ON Cat_Product.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductDescription FROM CAT_ProductDescription INNER JOIN #ratetable ON CAT_ProductDescription.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductIdentifier FROM CAT_ProductIdentifier INNER JOIN #ratetable ON CAT_ProductIdentifier.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductImage FROM CAT_ProductImage INNER JOIN #ratetable ON CAT_ProductImage.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductToLinkGroup FROM CAT_ProductToLinkGroup INNER JOIN #ratetable ON CAT_ProductToLinkGroup.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM Product_Association FROM Product_Association INNER JOIN #ratetable ON Product_Association.ToProduct = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_StoreRangeProduct FROM CAT_StoreRangeProduct INNER JOIN #ratetable ON CAT_StoreRangeProduct.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductStoreRange FROM CAT_ProductStoreRange INNER JOIN #ratetable ON CAT_ProductStoreRange.Product_Id = #ratetable.Product_Id").ExecuteUpdate();
            Session.CreateSQLQuery("DELETE FROM CAT_ProductCalorie FROM CAT_ProductCalorie INNER JOIN #ratetable ON CAT_ProductCalorie.Product_Id = #ratetable.Product_Id").ExecuteUpdate();

            return deletedCount;
        }

        private ICriteria CreateExternalCriteria(string type, string alias)
        {
            var criteria = Session.CreateCriteria(type, alias);

            criteria.CreateCriteria("PriceSorting.Product", "Consumable").SetProjection(Projections.GroupProperty("PriceSorting.Product"));

            return criteria;
        }

        private ICriteria CreateProductCriteria()
        {
            const string type = "Retalix.StoreServices.BusinessComponents.Product.Legacy.Item.Consumable";
            const string alias = "Consumable";
            return Session.CreateCriteria(type, alias);
        }

        private ICriteria CreateProductClassificationCriteria()
        {
            return Session.CreateCriteria<ProductClassification>("ProductClassification");
        }
    }
}
