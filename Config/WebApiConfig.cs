using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Http;
using log4net;
using NCR.RetailGateway.Infrastructure.Configuration.Configuration;

namespace NCR.RetailGateway.Services.Config
{
    public class WebApiConfig
    {
        public static List<SensitiveRequestIdentifier> SensitiveRequests = new List<SensitiveRequestIdentifier>();
        private static readonly ILog Log = LogManager.GetLogger("WebApiConfig");

        public static void Configure(HttpRouteCollection routes)
        {
            ExtensionConfigure(routes);
            routes.MapHttpRoute("SaleTransactionBegin", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}", new { controller = "saletransactions", storeId = RouteParameter.Optional, action = "BeginTransaction" });
            routes.MapHttpRoute("SaleTransactionBeginWithCustomer", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/customer/{shopperId}", new { controller = "saletransactions", storeId = RouteParameter.Optional, action = "BeginTransactionWithCustomer" });
            routes.MapHttpRoute("SaleTransactionLineConfirm", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineItem/{itemId}/confirmations", new { controller = "saletransactionline", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, itemId = RouteParameter.Optional, action = "confirm" });
            routes.MapHttpRoute("SaleTransactionLineTareModifier", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineSequence/{sequenceNumber}/taremodifier", new { controller = "saletransactionline", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, sequenceNumber = RouteParameter.Optional, action = "taremodifier" });
            routes.MapHttpRoute("SaleTransactionLineNote", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineSequence/{sequenceNumber}/notes", new { controller = "saletransactionline", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, sequenceNumber = RouteParameter.Optional, action = "note" });
            routes.MapHttpRoute("SaleTransactionLineCoupon", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineSequence/{sequenceNumber}/coupons", new { controller = "saletransactionline", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, sequenceNumber = RouteParameter.Optional, action = "coupon" });
            routes.MapHttpRoute("SaleTranscationLineBulk", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/LineItems", new { controller = "saletransactionline", action = "LineItems" });
            routes.MapHttpRoute("SaleTransactionLine", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineItem/{itemId}", new { controller = "saletransactionline", action = "" });
            routes.MapHttpRoute("SaleTransactionLineSequence", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/lineSequence/{sequenceNumber}", new { controller = "saletransactionline", action = "" });
            routes.MapHttpRoute("SaleTransactionPayEps", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/payments/eps", new { controller = "saletransactionscheckout", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "epspayment" });
            routes.MapHttpRoute("SaleTransactionEndTrip", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/endtrip", new { controller = "saletransactionscheckout", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "endtrip" });
            routes.MapHttpRoute("SaleTransactionLoyaltyRedemption", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/LoyaltyRedemptions", new { controller = "saletransactionscheckout", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "LoyaltyRedemption" });
            routes.MapHttpRoute("SaleTransactionPayWithPoints", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/PointsPayments", new { controller = "saletransactionscheckout", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "PayWithPoints" });
            routes.MapHttpRoute("SaleTransactionRefundPoints", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/PointsRefunds", new { controller = "saletransactionscheckout", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "RefundPoints" });
            routes.MapHttpRoute("SaleTransaction", RestApiConstants.FullPrefix + "selling/v1/saletransactions/{storeId}/{transactionId}/{action}", new { controller = "saletransactions", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "" });

            routes.MapHttpRoute("SaleTransactionLineV3", RestApiConstants.FullPrefix + "selling/v3/saletransactions/{storeId}/{transactionId}/lineItem/{itemId}", new { controller = "saletransactionlinev3", actionRuleInputData = RouteParameter.Optional, action = "" });
            routes.MapHttpRoute("SaleTransactionLineSequenceV3", RestApiConstants.FullPrefix + "selling/v3/saletransactions/{storeId}/{transactionId}/lineSequence/{sequenceNumber}", new { controller = "saletransactionlinev3", actionRuleInputData = RouteParameter.Optional, action = "" });
            routes.MapHttpRoute("SaleTransactionV3", RestApiConstants.FullPrefix + "selling/v3/saletransactions/{storeId}/{transactionId}/{action}", new { controller = "saletransactionsv3", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, actionRuleInputData = RouteParameter.Optional, action = "" });

            routes.MapHttpRoute("Receipt", RestApiConstants.FullPrefix + "electronicjournal/v1/receipts/{storeId}/{transactionBarcode}", defaults: new { action = "", controller = "receipt" });
            routes.MapHttpRoute("Restrictions", RestApiConstants.FullPrefix + "selling/v1/restrictions/{storeId}/{transactionId}", defaults: new { controller = "restrictions" });

            routes.MapHttpRoute("ShoppingListLine", RestApiConstants.FullPrefix + "shoppinglist/v1/shoppinglists/{shoppingListId}/lines/{lineId}/{action}", defaults: new { controller = "ShoppingListLine", action = "", lineId = RouteParameter.Optional });
            routes.MapHttpRoute("ShoppingListsDefault", RestApiConstants.FullPrefix + "shoppinglist/v1/shoppinglists/default", defaults: new { controller = "ShoppingLists", action = "default" });
            routes.MapHttpRoute("ShoppingList", RestApiConstants.FullPrefix + "shoppinglist/v1/shoppinglists/{shoppingListId}/{action}", defaults: new { controller = "ShoppingList", action = "" });

            routes.MapHttpRoute("CatalogStoreItem", RestApiConstants.FullPrefix + "catalog/v1/catalog/items/{barcode}/store/{storeId}", defaults: new { controller = "CatalogItem", action = "StoreItem" });
            routes.MapHttpRoute("CatalogItem", RestApiConstants.FullPrefix + "catalog/v1/catalog/items/{barcode}", defaults: new { controller = "CatalogItem", action = "" });
            routes.MapHttpRoute("CatalogItemsBySearchText", RestApiConstants.FullPrefix + "catalog/v1/catalog/items", defaults: new { controller = "CatalogItem", action = "ItemsBySearchText" });
            routes.MapHttpRoute("CatalogRelativeItems", RestApiConstants.FullPrefix + "catalog/v1/catalog/relativeitems", defaults: new { controller = "CatalogItem", action = "RelativeItems" });

            routes.MapHttpRoute("PromotionByItem", RestApiConstants.FullPrefix + "promotion/v1/promotions", defaults: new { controller = "promotion", action = "byItem" });
            routes.MapHttpRoute("PromotionById", RestApiConstants.FullPrefix + "promotion/v1/promotions/{promotionId}", defaults: new { controller = "promotion", action = "" });
            routes.MapHttpRoute("PromotionApprovalStatus", RestApiConstants.FullPrefix + "promotion/v1/promotions/{storeId}/{transactionId}/approvalStatus", new { controller = "promotion", storeId = RouteParameter.Optional, transactionId = RouteParameter.Optional, action = "approvalStatus" });

            SensitiveRequests.Add(new SensitiveRequestIdentifier
            {
                ControllerName = "creditcards",
                MethodType = "put"
            });

            routes.MapHttpRoute("CreditCardTypeImage", RestApiConstants.FullPrefix + "tender/v1/creditcardtype/{retailer}/{externalId}", new { controller = "CreditCardType" });

            routes.MapHttpRoute("OffersSearch", RestApiConstants.FullPrefix + "promotion/v1/offers/search", defaults: new { controller = "Offers", action = "" });
            routes.MapHttpRoute("OffersCategories", RestApiConstants.FullPrefix + "promotion/v1/offers/categories", defaults: new { controller = "Offers", action = "categories" });
            routes.MapHttpRoute("OffersImage", RestApiConstants.FullPrefix + "promotion/v1/offers/images", defaults: new { controller = "Offers", action = "images" });
            routes.MapHttpRoute("OffersRegistered", RestApiConstants.FullPrefix + "promotion/v1/offers/registered", defaults: new { controller = "Offers", action = "registered" });
            routes.MapHttpRoute("OffersRegister", RestApiConstants.FullPrefix + "promotion/v1/offers/{promotionId}/registration", defaults: new { controller = "Offers", action = "registration", promotionId = RouteParameter.Optional });
            routes.MapHttpRoute("offersById", RestApiConstants.FullPrefix + "promotion/v1/offers/{promotionId}", defaults: new { controller = "Offers", action = "byId" });

            routes.MapHttpRoute("OrderProcess", RestApiConstants.FullPrefix + "v1/OrderProcess/{action}/{shoppingListId}", defaults: new { controller = "OrderProcess" });
            routes.MapHttpRoute("AccountPrograms", RestApiConstants.FullPrefix + "customer/v1/accountprograms/{loyaltyProgramId}", defaults: new { controller = "AccountPrograms", action = "" });
            routes.MapHttpRoute("AccountProgramsVisual", RestApiConstants.FullPrefix + "customer/v1/accountprogramsvisual/{loyaltyProgramId}", defaults: new { controller = "AccountProgramsVisual", action = "", loyaltyProgramId = RouteParameter.Optional });

            routes.MapHttpRoute("Order", RestApiConstants.FullPrefix + "selling/v1/customerorder/", defaults: new { controller = "CustomerOrder", action = "" });
            routes.MapHttpRoute("OrderLastActive", RestApiConstants.FullPrefix + "selling/v1/customerorder/{storeId}/lastActive", defaults: new { controller = "CustomerOrder", action = "lastActive" });
            routes.MapHttpRoute("OrderWithId", RestApiConstants.FullPrefix + "selling/v1/customerorder/{transactionId}/store/{storeId}", defaults: new { controller = "CustomerOrder", action = "Update" });

            routes.MapHttpRoute("DataDecode", RestApiConstants.FullPrefix + "utils/v1/data", defaults: new { controller = "Data" });

            routes.MapHttpRoute("SelfScanStartup", RestApiConstants.FullPrefix + "selling/v1/selfScan/startup", defaults: new { controller = "SelfScan", action = "startup" });
            routes.MapHttpRoute("SelfScan", RestApiConstants.FullPrefix + "selling/v1/selfScan/{storeId}/{action}", defaults: new { controller = "SelfScan", storeId = RouteParameter.Optional, action = "" });

            routes.MapHttpRoute("Account", RestApiConstants.FullPrefix + "customer/v1/account/{action}", defaults: new { controller = "Account", action = "" });

            routes.MapHttpRoute("AccountAffiliations", RestApiConstants.FullPrefix + "customer/v1/AccountAffiliations/{action}", defaults: new { controller = "AccountAffiliations", action = "" });
            routes.MapHttpRoute("ClientConfigGeneral", RestApiConstants.FullPrefix + "client/v1/ClientConfig/{action}", defaults: new { controller = "ClientConfig", action = "" });
            routes.MapHttpRoute("LocalizedListGeneral", RestApiConstants.FullPrefix + "utils/v1/localizedlists", new { controller = "LocalizedList", action = "" });
            routes.MapHttpRoute("LocalizedResourceGeneral", RestApiConstants.FullPrefix + "utils/v1/localizedresources/{action}", new { controller = "LocalizedResource", action = "" });

            routes.MapHttpRoute("ProductDescriptions", RestApiConstants.FullPrefix + "catalog/v1/stores/{storeId}/products/descriptions", defaults: new { controller = "Product", action = "descriptions" });
            routes.MapHttpRoute("ProductCategories", RestApiConstants.FullPrefix + "catalog/v1/stores/{storeId}/products/categories", defaults: new { controller = "Product", action = "categories" });

            routes.MapHttpRoute("ProductByTransactionId", RestApiConstants.FullPrefix + "catalog/v1/stores/{storeId}/saletransaction/{transactionstoreid}/{transactionid}/products", defaults: new { controller = "Product", action = "ByTransactionId" });
            routes.MapHttpRoute("ProductByCategory", RestApiConstants.FullPrefix + "catalog/v1/stores/{storeId}/products", defaults: new { controller = "Product", action = "search", storeId = RouteParameter.Optional });
            routes.MapHttpRoute("ProductById", RestApiConstants.FullPrefix + "catalog/v1/stores/{storeId}/products/{id}", defaults: new { controller = "Product", action = "ById" });
            routes.MapHttpRoute("ReceiptLog", RestApiConstants.FullPrefix + "electronicjournal/v1/receipts/log", defaults: new { action = "log", controller = "receipt" });

            routes.MapHttpRoute("allSites", RestApiConstants.FullPrefix + "storeinfo/v1/sites/locations", new { controller = "sites", action = "locations" });
            routes.MapHttpRoute("sites", RestApiConstants.FullPrefix + "storeinfo/v1/sites/{id}/{action}", new { controller = "sites", id = RouteParameter.Optional, action = "" });

            routes.MapHttpRoute("ShoppingLists", RestApiConstants.FullPrefix + "shoppinglist/v1/shoppinglists/", defaults: new { controller = "ShoppingLists", action = "" });

            routes.MapHttpRoute("TransactionLog", RestApiConstants.FullPrefix + "electronicjournal/v1/transactionlogs/{action}", new { controller = "TransactionLog", action = "" });
            routes.MapHttpRoute("Version", RestApiConstants.FullPrefix + "applicationdata/v1/version", new { controller = "Version", action = "" });

            routes.MapHttpRoute("Rescan", RestApiConstants.FullPrefix + "rescan/v1/rescan/{action}", new { controller = "Rescan", action = "" });

            routes.MapHttpRoute("Worker", RestApiConstants.FullPrefix + "selling/v1/Worker/{action}", new { controller = "Worker", action = "" });

            SensitiveRequests.Add(new SensitiveRequestIdentifier
            {
                ControllerName = "account",
                MethodName = "resetpassword"
            });

            SensitiveRequests.Add(new SensitiveRequestIdentifier
            {
                ControllerName = "account",
                MethodName = "changepassword"
            });
        }

        public static void ExtensionConfigure(HttpRouteCollection routes)
        {
            try
            {
                var extensions = WebApiExtensionsConfiguration.Extensions;

                for (var i = 0; i < extensions.Count; i++)
                {
                    var extensionConfiguration = extensions[i];
                    var assembly = Assembly.Load(extensionConfiguration.AssemblyName);
                    var type = assembly.GetType(extensionConfiguration.ClassName);

                    if (type == null) continue;

                    var instance = (IExtensionWebApiConfig)Activator.CreateInstance(type);
                    instance.ConfigureRoute(routes, SensitiveRequests);
                    
                    Log.Debug(string.Format("Extension {0} loaded correctly", extensionConfiguration.AssemblyName));
                }
            }
            catch (Exception e)
            {
                Log.Debug("Could not load extension", e);
            }
        }

        public class SensitiveRequestIdentifier
        {
            public string MethodName = string.Empty;
            public string ControllerName = string.Empty;
            public string MethodType = string.Empty;
        }
    }
}