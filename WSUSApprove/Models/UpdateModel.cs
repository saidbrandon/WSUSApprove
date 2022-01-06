using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSUSApprove.Models {
    [Serializable]
    public class UpdateModel {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public DateTime CreationDate { get; set; }
        public StringCollection ProductFamilyTitles { get; set; }
        public StringCollection ProductTitles { get; set; }
    }
}
