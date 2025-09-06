using System.Configuration;

namespace TransactionViewer.DataAccess
{
    public static class DbHelper
    {
        public static string ConnString =>
            ConfigurationManager.ConnectionStrings["TransactionDb"].ConnectionString;
    }
}
