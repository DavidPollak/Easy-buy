namespace NCR.RetailGateway.Model.Catalog
{
    public class SearchRequest
    {
        public string SearchText { get; set; }
        public int BlockSize { get; set; }
        public int BlockNumber { get; set; }
        public string StoreId { get; set; }
        public string Culture { get; set; }
        public string BusinessUnit { get; set; }

        public SearchRequest(string searchText, int blockSize, int blockNumber, string storeId, string culture)
        {
            SearchText = searchText;
            BlockSize = blockSize;
            BlockNumber = blockNumber;
            StoreId = storeId;
            Culture = culture;
        }
        
    }
}
