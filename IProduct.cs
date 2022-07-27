using System;
using System.Collections.Generic;
using Retalix.StoreServices.Model.Infrastructure.Globalization;

using Retalix.StoreServices.Model.Product.Associations;
using Retalix.StoreServices.Model.Product.Associations.Containment;
using Retalix.StoreServices.Model.Product.Calorie;
using Retalix.StoreServices.Model.Product.Hierarchy;
using Retalix.StoreServices.Model.Product.ProductAttribute;
using ILinkGroup = Retalix.StoreServices.Model.Product.Associations.Links.ILinkGroup;
using ObsoleteILinkGroup = Retalix.StoreServices.Model.Product.Associations.ILinkGroup;

namespace Retalix.StoreServices.Model.Product
{
    /// <summary>
    /// Represents goods or services in the retailer's catalog, either for selling, ordering, or any other use.
    /// <br />
    /// Product is the reference for associating information published by the manufacturer or service provider regarding any item in the catalog.
    /// <br />
    /// Products are usually labeled by one or more external identifiers, e.g. GTIN or EAN (See: http://www.gtin.info/index.html).
    /// Products are described by a set of attributes with values, the attributes may be complex or simple, but are usually fixed and not sensitive to the environmental conditions
    /// i.e. a can of coke contains the same amount of sugar regardless of the temperature or time at which it is sold. 
    /// </summary>
    /// <example>
    /// <code lang="cs" description="This example shows how to retrieve an existing product with code 123 of type EAN in the context of a business service.">
    /// <![CDATA[
    /// public class ProductLookupService
    /// {
    ///     private readonly IProductDao _productDao;
    /// 
    ///     public ProductLookupService(IProductDao productDao)
    ///     {
    ///         _productDao = productDao;
    ///     }
    /// 
    ///     public IProduct GetProduct(string code, string type)
    ///     {
    ///         Identifier identifier = new Identifier(code, type);
    ///         IProduct product = _productDao.GetByIdentifier(identifier);
    ///         return product;
    ///     }
    /// }
    /// 
    /// public class ProductLookupServiceExample
    /// {
    ///     const string code = "123";
    ///     const string type = "EAN";
    ///     
    ///     private readonly ProductLookupService _service;
    /// 
    ///     public ProductLookupServiceExample(IProductDao productDao)
    ///     {
    ///         _service = new ProductLookupService(productDao);
    ///     }
    /// 
    ///     public IProduct FindProductByIdentifier()
    ///     {
    ///          return _service.GetProduct(code,type);
    ///     }
    /// }
    /// ]]>
    /// </code>
    /// </example>
    /// <example>
    /// <code lang="cs" description="This example shows how to update an existing product's identifiers and save it in the context of a business service.">
    /// <![CDATA[
    /// public class UpdateProductIdentifiersService
    /// {
    ///     private readonly IProductDao _productDao;
    /// 
    ///     public UpdateProductIdentifiersService(IProductDao productDao)
    ///     {
    ///         _productDao = productDao;
    ///     }
    /// 
    ///     public IProduct UpdateProductIdentifiers(string productId, string code, string type)
    ///     {
    ///         IProduct product = _productDao.Get(productId); /*Pay attention that this is the product's entity key, 
    ///                                                         * it is used for retrieving a specific product by its unique key
    ///                                                         * and not used to lookup this product.*/
    /// 
    ///         Identifier newIdentifier = new Identifier(code, type);
    ///         product.Identifiers.Add(newIdentifier);
    ///         return product;
    ///     }
    /// }]]>
    /// </code>
    /// </example>
    ////[DocumentationReview("YuvalD", 2013, 1, 30, DocumentationQuality.Good, "medium documentation")]
    public interface IProduct 
    {
        /// <summary>
        /// Gets the unique key of the product entity in the system that cannot be change once you create it.
        /// <br />
        /// This key is used to identify product uniquely for maintenance purposes,
        /// for example, if you have a daily process that import(update) products from external system to your system,
        /// this key will be the reference to specific instance like ID\Passport for people.
        /// <br />
        /// With this key you can find the product and change his identifiers for example.
        /// </summary> 
        /// <remarks>
        /// <para>
        /// Note that in order to identify a product for business purposes (i.e. lookup) use the <see cref="IProduct.Identifiers">Identifiers</see> property.
        /// </para>
        /// </remarks>
        string EntityKey { get; }

        /// <summary>
        /// Gets or sets the supplied weight of the product.
        /// </summary>
        /// <value>A product's <see cref="Weight">Weight</see>. The default value is <see langword="null"/> - no weight provided.</value>
        /// <remarks>
        /// A weight could be used for reporting and for calculation of weight reduction, when product is container.<br/>
        /// Please note the limitations of <see cref="Weight">Weight</see> that means comparison and calculation of Weight are possible only within a single UnitOfMeasureType  
        /// </remarks>
        Weight Weight { get; set; }

        /// <summary>
        /// Gets a list of the product descriptions.
        /// </summary>        
        /// <value>A collection of <see cref="LocalizedDescription">ProductDescription</see> that contains the product's descriptions, 
        /// a product can have multiple descriptions for different languages and types</value>
        IList<ProductDescription> Descriptions { get; }

        /// <summary>
        /// Gets a list of <see cref="Identifier">Identifier</see>s this product identify by.
        /// <br />
        /// Identifiers are used to look up product in a system, each identifier is unique in the system.
        /// For example, if you have product in the system with too identifiers (123,EAN) and (456,UPC) you can find this product by look up by identifier (123,EAN) or by identifier (456,UPC). 
        /// </summary>
        IList<Identifier> Identifiers { get; }

        /// <summary>
        /// Gets or sets the names of product images in a URL format.
        /// </summary>
        IList<string> ImageNames { get; }

        /// <summary>
        /// <para><b>Note: This property is non stable and could be removed in future versions</b></para> 
        /// Gets or sets the flag that indicates that the product is Non-Merchandise product.
        /// <br />
        /// Non-Merchandise products include items such as postage stamps, gift certificates, gift cards, and bottle deposits.
        /// Non-Merchandise products could be treated differently in total order calculations. (Taxation, Tendering etc.)
        /// </summary>
        bool IsNonMerchandise { get; set; }


        /// <summary>
        /// <para><b>Note: This property is non stable and could be removed in future versions</b></para> 
        /// Gets or sets a flag, which indicates if manual entry of tare percentage is allowed.
        /// <br />
        /// When True, it is possible to manually enter percentage of product's weight that is actually tare.
        /// Tare weight can be reduced from gross product weight to get net product weight.
        /// In case that tare weight is relative (e.g. ice weight in frozen fish), manual entry of tare percentage is required.
        /// </summary>
        bool IsManualPercentageEnable { get; set; }

        ///<summary>
        /// Gets a collection of <see cref="IAssociation">IAssociation</see>s, this product is associated to.
        /// <br />
        /// This includes the links associated to this product using a <see cref="ILinkGroup"/>.
        /// The collection may include instances of both <see cref="LinkAssociation"/> and <see cref="ContainmentAssociation"/>.
        /// Note that <see cref="LinkAssociation"/> is obsolete. Links should be handled by <see cref="IProduct.LinkedGroups"/>.
        ///</summary>
        ICollection<IAssociation> AssociatedTo { get; }

        /// <summary>
        /// Get a collection of products that are associated to this product.
        /// <br />
        /// The collection may include instances products linked to this one using either <see cref="LinkAssociation"/> or <see cref="ContainmentAssociation"/>.
        /// It will not include associations made through the <see cref="ILinkGroup"/>s.
        /// Note that <see cref="LinkAssociation"/> is obsolete. Links should be handled by <see cref="IProduct.LinkedGroups"/>.
        /// </summary>
        IEnumerable<IProduct> AssociatedBy { get; }

        /// <summary>
        /// Gets a collection of <see cref="ILinkGroup">ILinkGroup</see>s this product points to.
        /// <br />
        /// All products in these groups will appear as <see cref="LinkAssociation">LinkAssociation</see>s 
        /// with the groups' <see cref="LinkCategory">LinkCategory</see> on it.
        /// </summary>
        [Obsolete("The property is obsolete since 10.5.0 and will be discontinued from version 10.5.3. Use LinkedGroups property instead.")]
        ICollection<ObsoleteILinkGroup> LinkGroups { get; }

        ICollection<ILinkGroup> LinkedGroups { get; }

        /// <summary>
        /// Gets the <see cref="IMerchandiseCategory">IMerchandiseCategory</see> of this product.
        /// </summary>
        IMerchandiseCategory MerchandiseCategory { get; set;  }

        /// <summary>
        /// Gets the <see cref="decimal">TareWeightPercentages</see> of this product.
        /// </summary>
        ICollection<decimal> TareWeightPercentages { get; }

        /// <summary>
        /// Gets dictionary of <see cref="IProductAttribute">attributes</see> of this product. 
        /// the key of the dictionary must be the same as the key inside <see cref="IProductAttribute">the attribute</see>
        /// </summary>
        ICollection<IProductAttribute> ProductAttributes { get; }

        /// <summary>
        /// Gets a list of the product descriptions.
        /// </summary>
        /// <param name="type">Type description</param>
        /// <returns></returns>
        IEnumerable<ProductDescription> GetDescriptions(string type);

        /// <summary>
        /// Gets or sets product calories info
        /// </summary>
        ProductCalories Calories { get; set; }
    }
}
