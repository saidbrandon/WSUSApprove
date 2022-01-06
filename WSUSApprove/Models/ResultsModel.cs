using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSUSApprove.Models {
    [Serializable]
    public class ResultsModel {
        public DateTime Date { get; set; }
        public string WSUSServer { get; set; }
        public ushort WSUSPort { get; set; }
        public bool WSUSUseSSL { get; set; }
        public StringCollection Products { get; set; }
        public string TitleFilters { get; set; }
        public string Group { get; set; }
        public Guid GroupId { get; set; }
        public int ApprovalDelay { get; set; }
        public Dictionary<Guid, UpdateModel> Data { get; set; }
    }
}
