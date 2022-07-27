namespace NCR.RetailGateway.Services.Config
{
    public static class RestApiConstants
    {
        public static string RestApiUrlPrefix
        {
            get { return "api"; }
        }

        public static string FullPrefix
        {
            get { return string.Format("{0}/", RestApiUrlPrefix); }
        }
    }
}
