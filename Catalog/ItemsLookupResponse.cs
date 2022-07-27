using System.Collections.Generic;
using NCR.RetailGateway.DataContracts.POS;

namespace NCR.RetailGateway.Model.Catalog
{
    
    public class ItemsLookupResponse
    {
        public ItemsLookupResponse()
        {
            Items = new Dictionary<string, Item>();
        }

        
        public Dictionary<string, Item> Items { set; get; }
    }
}