using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace OneNoteDictationTool
{
    public partial class LogForm : Form
    {
        public LogForm()
        {
            InitializeComponent();
        }

        public void UpdateLogMessages(List<string> pLogMessages)
        {
            tboxLog.Clear();
            var log = new StringBuilder();
            foreach (var msg in pLogMessages)
                log.AppendLine(msg);

            tboxLog.Text = log.ToString();
        }
    }
}
