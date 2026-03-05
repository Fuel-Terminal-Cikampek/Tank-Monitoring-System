using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMS.AutoBackupDatabase
{
    public class AppConfig
    {
        public int interval { get; set; }
        public int dataAmount { get; set; }      
        public string backupMode { get; set; } = "Daily";
        public string timeStart { get; set; }
        public string timeEnd { get; set; }
        public int dayOfMonth { get; set; } = 1; 
        public string timeBackup { get; set; } = "00:00:00"; 
        public string exportPath { get; set; }
        
        // ✅ NEW: Auto-delete setelah backup
        public bool autoDeleteAfterBackup { get; set; } = false; // Default: TIDAK hapus
        
        // ✅ NEW: Retention period (berapa bulan data yang disimpan)
        public int retentionMonths { get; set; } = 3; // Default: simpan 3 bulan terakhir
    }
}
