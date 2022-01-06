using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace WSUSApprove {
    public sealed class CommandLineOptions {
        [Option("ShowAllGroups", Required = false, HelpText = "Displays All WSUS Groups instead of just the predefined groups.")]
        public bool ShowAllGroups { get; set; }

        [Option("AskAnyway", Required = false, HelpText = "If specified, will ask to approve updates when auto approving updates is set to no. This parameter will prompt for approval on every needed update detected instead of approving none, or all updates.")]
        public bool AskAnyway { get; set; }

        [Option("ApprovalOverride", Required = false, HelpText = "If specified, you will be prompted to enter the approval delay in days or weeks regardless if it's previously defined or not.")]
        public bool ApprovalOverride { get; set; }

        [Option("UsePreviousScan", Required = false, HelpText = "If specified and detected, script will not rescan WSUS Server for needed updates. This is useful if the first run is used to only display needed updates, then run again to approve updates.")]
        public bool UsePreviousScan { get; set; }

        [Option("WhatIf", Required = false, HelpText = "If specified, and you approve an update, the update will display information as if it was approved, but doesn't actually approve.")]
        public bool WhatIf { get; set; }
    }
}
