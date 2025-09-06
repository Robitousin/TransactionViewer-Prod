using System.Drawing.Printing;

namespace TransactionViewer
{
    public interface IPrintManager
    {
        void PrintDocument_PrintPage(object sender, PrintPageEventArgs e);
    }
}

