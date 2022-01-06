using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UpdateServices.Administration;

namespace WSUSApprove.Models {
    public class WsusComputerTargetGroup {
        private IComputerTargetGroup computerTargetGroup;
        private long memberCount;
        private List<WsusComputerTargetGroup> computerTargetChildGroups;

        internal WsusComputerTargetGroup(IComputerTargetGroup computerTargetGroup) {
            if (computerTargetGroup == null)
                throw new ArgumentNullException(nameof(computerTargetGroup));
            this.computerTargetGroup = computerTargetGroup;
        }
        public Guid Id {
            get {
                return computerTargetGroup.Id;
            }
        }
        public IComputerTargetGroup ComputerTargetGroup {
            get {
                return computerTargetGroup;
            }
            internal set {
                computerTargetGroup = value;
            }
        }
        public string Name {
            get {
                return computerTargetGroup.Name;
            }
        }
        public long MemberCount {
            get {
                return memberCount;
            }
            set {
                memberCount = value;
            }
        }
        public List<WsusComputerTargetGroup> ComputerTargetChildGroups {
            get {
                return computerTargetChildGroups;
            }
            set {
                computerTargetChildGroups = value;
            }
        }
        public bool IsProtected {
            get {
                if (!(Id == ComputerTargetGroupId.AllComputers))
                    return Id == ComputerTargetGroupId.UnassignedComputers;
                return true;
            }
        }
        public List<WsusComputerTargetGroup> GetChildTargetGroupsSortedByName() {
            List<WsusComputerTargetGroup> childTargetGroups = this.GetChildTargetGroups();
            childTargetGroups.Sort((IComparer<WsusComputerTargetGroup>)new WsusComputerTargetGroup.WsusComputerTargetGroupSorter());
            return childTargetGroups;
        }
        private List<WsusComputerTargetGroup> GetChildTargetGroups() {
            throw new NotImplementedException();
        }
        public class WsusComputerTargetGroupSorter : IComparer<WsusComputerTargetGroup> {
            public int Compare(WsusComputerTargetGroup firstItem, WsusComputerTargetGroup secondItem) {
                if (firstItem == null)
                    return secondItem != null ? 1 : 0;
                if (secondItem == null)
                    return -1;
                if (firstItem.Id == secondItem.Id)
                    return 0;
                if (firstItem.Id == ComputerTargetGroupId.AllComputers)
                    return -1;
                if (secondItem.Id == ComputerTargetGroupId.AllComputers)
                    return 1;
                if (firstItem.Id == ComputerTargetGroupId.UnassignedComputers)
                    return -1;
                if (secondItem.Id == ComputerTargetGroupId.UnassignedComputers)
                    return 1;
                return string.Compare(firstItem.Name, secondItem.Name, false, CultureInfo.CurrentCulture);
            }
        }
    }
}
