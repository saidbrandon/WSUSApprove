using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alba.CsConsoleFormat;
using CommandLine;
using Microsoft.UpdateServices.Administration;
using WSUSApprove.Models;

namespace WSUSApprove {
    internal class Program {
        static void Main(string[] args) {
            DateTime startTime = DateTime.Now;
            ResultsModel Results = null;
            IUpdateServer UpdateServer = null;
            bool autoApproveUpdates = false;
            IComputerTargetGroup approvalGroup = null;
            string WSUSServer = Properties.Settings.Default.WSUSServer;
            ushort WSUSPort = Properties.Settings.Default.WSUSPort;
            bool WSUSUseSSL = Properties.Settings.Default.WSUSUseSSL;
            bool Secure = WSUSUseSSL == true ? true : false;
            CommandLineOptions options = new CommandLineOptions();
            var parser = new Parser(cfg => cfg.CaseSensitive = false);
            var results = parser.ParseArguments<CommandLineOptions>(args)
                .WithParsed<CommandLineOptions>(opts => options = opts)
                .WithNotParsed<CommandLineOptions>((errs) => HandleParseError(errs));

            if (!File.Exists(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath)) {
                try {
                    Properties.Settings.Default.Upgrade();
                    Properties.Settings.Default.Save();
                } catch {
                }
            }

            if (options.UsePreviousScan == false) {
                Properties.Settings.Default.PreviousScan = "";
                Properties.Settings.Default.Save();
            }

            if (Properties.Settings.Default.PreviousScan != String.Empty) {
                try {
                    Results = (ResultsModel)DeserializeResultsObject(Properties.Settings.Default.PreviousScan);
                } catch {
                }
            }

            if (Results != null) {
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.WriteLine("| Previous Scan Detected                                                          |");
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.WriteLine("Server:          {0}", String.Format("{0}://{1}:{2}", Results.WSUSUseSSL == true ? "https" : "http", Results.WSUSServer, Results.WSUSPort));
                Console.WriteLine("Date:            {0}", Results.Date);
                Console.WriteLine("Product(s):      {0}", StringCollectionJoin(Results.Products));
                Console.WriteLine("Title Filter(s): {0}", Results.TitleFilters);
                Console.WriteLine("Group:           {0}", Results.Group);
                Console.WriteLine("Approval Delay:  {0} day(s)", Results.ApprovalDelay);
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.Write("Re-Use Results? (Y or 1|N or 0) [N]: ");
                string previousInput = Console.ReadLine();
                if (previousInput != String.Empty && (previousInput.ToUpper() == "Y" || previousInput == "1")) {
                    Console.Write("Would you like to change the Product Selection? (Y or 1|N or 0) [N]: ");
                    string changeProductsInput = Console.ReadLine();
                    if (changeProductsInput != String.Empty && (changeProductsInput.ToUpper() == "Y" || changeProductsInput == "1")) {
                        Results.Products = GetProducts();
                    }
                    Results.TitleFilters = GetTitleFilters(displayHeader: false, previousResults: Results.TitleFilters);
                    Console.Write("Would you like to change the Approval Delay? (Y or 1|N or 0) [N]: ");
                    string approvalDelayInput = Console.ReadLine();
                    if (approvalDelayInput != String.Empty && (approvalDelayInput.ToUpper() == "Y" || approvalDelayInput == "1")) {
                        int approvalDelayCheck = GetApprovalDelay();
                        if (approvalDelayCheck >= 0) {
                            Results.ApprovalDelay = approvalDelayCheck;
                        } else {
                            Console.WriteLine("Invalid Input. Ignoring...");
                        }
                    }
                    Console.Write("Would you like to Auto Approve Updates? (Y or 1|N or 0) [N]: ");
                    string autoApprovePreviousInput = Console.ReadLine();
                    if (autoApprovePreviousInput != String.Empty && (autoApprovePreviousInput.ToUpper() == "Y" || autoApprovePreviousInput == "1")) {
                        autoApproveUpdates = true;
                    }
                } else {
                    Environment.Exit(0);
                }
            } else {
                Int32 MaxThreads = Environment.ProcessorCount;
                Dictionary<int, int> ApprovalRings = new Dictionary<int, int>();
                ComputerTargetCollection Computers;

                Hashtable configurationRings = (Hashtable)ConfigurationManager.GetSection("ApprovalRings");
                foreach (DictionaryEntry configurationRing in configurationRings) {
                    if (int.TryParse(configurationRing.Key.ToString(), out int configurationRingKey)) {
                        if (int.TryParse(configurationRing.Value.ToString(), out int configurationRingValue)) {
                            ApprovalRings.Add(configurationRingKey, configurationRingValue);
                        }
                    }
                }

                Hashtable Groups = (Hashtable)ConfigurationManager.GetSection("Groups");

                try {
                    Console.WriteLine("Connecting to {0} on port {1} {2}", WSUSServer, WSUSPort, WSUSUseSSL == true ? "using SSL" : "using HTTP");
                    UpdateServer = AdminProxy.GetUpdateServer(WSUSServer, Secure, WSUSPort);
                    Console.WriteLine("Connected to " + UpdateServer.Name.ToString() + " [WSUS v." + UpdateServer.Version.ToString() + "]");
                } catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                    Environment.Exit(1);
                }
                Console.WriteLine("Gathering Group Information, Please Wait...");
                ComputerTargetGroupCollection AllGroupsTGC = UpdateServer.GetComputerTargetGroups();
                IComputerTargetGroup AllComputersGroup = UpdateServer.GetComputerTargetGroup(ComputerTargetGroupId.AllComputers);
                ComputerTargetGroupCollection TGC = AllComputersGroup.GetChildTargetGroups();
                ComputerTargetScope CTS = new ComputerTargetScope();
                List<WsusComputerTargetGroup> wsusGroups = new List<WsusComputerTargetGroup>();

                using (var progress = new ProgressBar(DateTime.Now)) {
                    Int32 progressCount = 0;
                    progress.Report(new ProgressData { _PercentCompleted = (double)progressCount / AllGroupsTGC.Count, _CompletedCount = progressCount, _TotalCount = AllGroupsTGC.Count, _ConsoleWidth = Console.WindowWidth });
                    WsusComputerTargetGroup wsusGroup = new WsusComputerTargetGroup(AllComputersGroup);
                    CTS.IncludeDownstreamComputerTargets = true;
                    CTS.ComputerTargetGroups.Add(AllComputersGroup);
                    wsusGroup.MemberCount = UpdateServer.GetComputerTargets(CTS).Count;
                    wsusGroups.Add(wsusGroup);
                    Parallel.ForEach(TGC.OfType<IComputerTargetGroup>(), new ParallelOptions { MaxDegreeOfParallelism = 1 }, TargetGroup => {
                        wsusGroup = new WsusComputerTargetGroup(TargetGroup);
                        CTS = new ComputerTargetScope();
                        CTS.IncludeDownstreamComputerTargets = true;
                        CTS.ComputerTargetGroups.Add(UpdateServer.GetComputerTargetGroup(TargetGroup.Id));
                        wsusGroup.MemberCount = UpdateServer.GetComputerTargets(CTS).Count;
                        if (TargetGroup.Name != "All Computers" && TargetGroup.Name != "Unassigned Computers") {
                            List<WsusComputerTargetGroup> childComputerTargetGroups = new List<WsusComputerTargetGroup>();
                            foreach (IComputerTargetGroup child in TargetGroup.GetChildTargetGroups()) {
                                WsusComputerTargetGroup wsusChildGroup = new WsusComputerTargetGroup(child);
                                CTS = new ComputerTargetScope();
                                CTS.IncludeDownstreamComputerTargets = true;
                                CTS.ComputerTargetGroups.Add(UpdateServer.GetComputerTargetGroup(child.Id));
                                wsusChildGroup.MemberCount = UpdateServer.GetComputerTargets(CTS).Count;
                                childComputerTargetGroups.Add(wsusChildGroup);
                                Interlocked.Increment(ref progressCount);
                                progress.Report(new ProgressData { _PercentCompleted = (double)progressCount / AllGroupsTGC.Count, _CompletedCount = progressCount, _TotalCount = AllGroupsTGC.Count, _ConsoleWidth = Console.WindowWidth });
                            }
                            childComputerTargetGroups.Sort((IComparer<WsusComputerTargetGroup>)new WsusComputerTargetGroup.WsusComputerTargetGroupSorter());
                            wsusGroup.ComputerTargetChildGroups = childComputerTargetGroups;
                        }
                        wsusGroups.Add(wsusGroup);
                        Interlocked.Increment(ref progressCount);
                        progress.Report(new ProgressData { _PercentCompleted = (double)progressCount / AllGroupsTGC.Count, _CompletedCount = progressCount, _TotalCount = AllGroupsTGC.Count, _ConsoleWidth = Console.WindowWidth });
                    });
                }
                StringCollection ProductFamilies = GetProducts();
                string titleFilter = GetTitleFilters(displayHeader: true);

                wsusGroups.Sort((IComparer<WsusComputerTargetGroup>)new WsusComputerTargetGroup.WsusComputerTargetGroupSorter());
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.WriteLine("| Groups                                                                          |");
                Console.WriteLine("-----------------------------------------------------------------------------------");
                int totalGroupCount = 0;
                int approvalDelay = -1;
                Dictionary<int, WsusComputerTargetGroup> sortedWsusGroups = new Dictionary<int, WsusComputerTargetGroup>();
                int longestGroupLength = 0;
                foreach (WsusComputerTargetGroup group in wsusGroups) {
                    if (group.Name.Length > longestGroupLength) {
                        longestGroupLength = group.Name.Length;
                    }
                }
                foreach (WsusComputerTargetGroup group in wsusGroups) {
                    approvalDelay = LookupApprovalDelay(Groups[group.Name]?.ToString(), ApprovalRings);
                    if (Groups.ContainsKey(group.Name) || options.ShowAllGroups == true) {
                        if (group.Name != "All Computers" && group.Name != "Unassigned Computers") {
                            Console.WriteLine(String.Format("{0,-47}{1,-16}{2,20}", totalGroupCount + group.Name.Length > 47 ? String.Format("{0}...", String.Format("{0} - {1}", totalGroupCount, group.Name).Substring(0, 43)) : String.Format("{0} - {1}", totalGroupCount, group.Name), ConvertApprovalDelay(approvalDelay), String.Format("{0:N0} members", group.MemberCount)));
                            sortedWsusGroups.Add(totalGroupCount, group);
                            totalGroupCount += 1;
                            if (group.ComputerTargetChildGroups != null && group.ComputerTargetChildGroups.Count >= 0) {
                                foreach (WsusComputerTargetGroup childGroup in group.ComputerTargetChildGroups) {
                                    if (Groups.ContainsKey(childGroup.Name) || options.ShowAllGroups == true) {
                                        approvalDelay = LookupApprovalDelay(Groups[childGroup.Name]?.ToString(), ApprovalRings);
                                        Console.WriteLine(String.Format("{0,-47}{1,-16}{2,20}", String.Format("{0} - {1}", totalGroupCount, childGroup.Name), ConvertApprovalDelay(approvalDelay), String.Format("{0:N0} members", childGroup.MemberCount)));
                                        sortedWsusGroups.Add(totalGroupCount, childGroup);
                                        totalGroupCount += 1;
                                    }
                                }
                            }
                        } else {
                            Console.WriteLine(String.Format("{0,-47}{1,-16}{2,20}", String.Format("{0} - {1}", totalGroupCount, group.Name), ConvertApprovalDelay(approvalDelay), String.Format("{0:N0} members", group.MemberCount)));
                            sortedWsusGroups.Add(totalGroupCount, group);
                            totalGroupCount += 1;
                        }
                    }
                }
                if (sortedWsusGroups.Count == 0) {
                    var group = wsusGroups.Where(x => x.Name == "All Computers").First();
                    sortedWsusGroups.Add(0, group);
                    Console.WriteLine(String.Format("{0,-47}{1,-16}{2,20}", totalGroupCount + group.Name.Length > 47 ? String.Format("{0}...", String.Format("{0} - {1}", totalGroupCount, group.Name).Substring(0, 43)) : String.Format("{0} - {1}", totalGroupCount, group.Name), ConvertApprovalDelay(approvalDelay), String.Format("{0:N0} members", group.MemberCount)));
                    totalGroupCount += 1;
                    group = wsusGroups.Where(x => x.Name == "Unassigned Computers").First();
                    sortedWsusGroups.Add(1, group);
                    Console.WriteLine(String.Format("{0,-47}{1,-16}{2,20}", totalGroupCount + group.Name.Length > 47 ? String.Format("{0}...", String.Format("{0} - {1}", totalGroupCount, group.Name).Substring(0, 43)) : String.Format("{0} - {1}", totalGroupCount, group.Name), ConvertApprovalDelay(approvalDelay), String.Format("{0:N0} members", group.MemberCount)));
                    totalGroupCount += 1;
                }
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.Write("Select which Group to update: [0 - {0}] [0]: ", totalGroupCount - 1);

                string input = Console.ReadLine();
                if (input == String.Empty) {
                    input = "0";
                }

                approvalDelay = -1;
                if (int.TryParse(input, out int result)) {
                    approvalGroup = UpdateServer.GetComputerTargetGroup(sortedWsusGroups[result].Id);
                    if (options.ApprovalOverride == false) {
                        if (int.TryParse(Groups[sortedWsusGroups[result]?.Name]?.ToString(), out int ringCheck)) {
                            if (ApprovalRings.ContainsKey(ringCheck)) {
                                approvalDelay = ApprovalRings[ringCheck];
                            } else {
                                approvalDelay = -1;
                            }
                        } else {
                            approvalDelay = -1;
                        }
                    } else {
                        approvalDelay = -1;
                    }

                    if (approvalDelay == -1) {
                        Console.WriteLine("-----------------------------------------------------------------------------------");
                        Console.WriteLine("| Invalid / Unknown / Manual Ring                                                 |");
                        Console.WriteLine("-----------------------------------------------------------------------------------");
                        approvalDelay = GetApprovalDelay();
                    }
                } else {
                    throw new Exception("Invalid Group");
                }
                if (approvalDelay == -1) {
                    approvalDelay = 0;
                }
                Console.WriteLine("Updates released on or before {0} will qualify for approval", DateTime.Now.AddDays(approvalDelay * -1).Date.ToString("d"));
                Console.Write("Would you like to Auto Approve Updates? (Y or 1|N or 0) [N]: ");
                string autoApproveInput = Console.ReadLine();

                autoApproveUpdates = false;
                if (autoApproveInput != String.Empty) {
                    if (autoApproveInput.ToUpper() == "Y" || autoApproveInput == "1") {
                        autoApproveUpdates = true;
                    }
                }

                CTS = new ComputerTargetScope();
                CTS.IncludeDownstreamComputerTargets = true;
                CTS.ComputerTargetGroups.Add(UpdateServer.GetComputerTargetGroup(sortedWsusGroups[result].Id));

                Computers = UpdateServer.GetComputerTargets(CTS);
                int count = 0;
                Dictionary<Guid, UpdateModel> Updates = new Dictionary<Guid, UpdateModel>();
                using (var progress = new ProgressBar(DateTime.Now)) {
                    progress.Report(new ProgressData { _PercentCompleted = (double)count / Computers.Count, _CompletedCount = count, _TotalCount = Computers.Count, _ConsoleWidth = Console.WindowWidth });
                    Parallel.ForEach(Computers.OfType<IComputerTarget>(), new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, Computer => {
                        List<UpdateModel> updates = new List<UpdateModel>();
                        try {
                            updates = GetUpdateInfo(UpdateServer, Computer);
                            foreach (UpdateModel update in updates) {
                                if (!Updates.ContainsKey(update.Id)) {
                                    Updates.Add(update.Id, update);
                                }
                            }
                        } catch {
                        }
                        Interlocked.Increment(ref count);
                        progress.Report(new ProgressData { _PercentCompleted = (double)count / Computers.Count, _CompletedCount = count, _TotalCount = Computers.Count, _ConsoleWidth = Console.WindowWidth });
                    });
                }
                Results = new ResultsModel { Date = startTime, WSUSServer = WSUSServer, WSUSPort = WSUSPort, WSUSUseSSL = WSUSUseSSL, Products = ProductFamilies, TitleFilters = titleFilter, Group = approvalGroup.Name, GroupId = approvalGroup.Id, ApprovalDelay = approvalDelay, Data = Updates };
                Properties.Settings.Default.PreviousScan = SerializeResultsObject(Results);
                Properties.Settings.Default.Save();
            }
            IOrderedEnumerable<KeyValuePair<Guid, UpdateModel>> orderedUpdates = Results.Data.OrderByDescending(u => u.Value.CreationDate);
            List<UpdateModel> visibleUpdates = new List<UpdateModel>();
            DateTime today = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0, 0, DateTimeKind.Local);
            Hashtable objectLengths = new Hashtable();
            objectLengths["CreationDate"] = 13;
            objectLengths["ProductFamily"] = 14;
            objectLengths["ProductTitle"] = 13;
            objectLengths["Title"] = 5;

            foreach (KeyValuePair<Guid, UpdateModel> update in orderedUpdates) {
                DateTime creationDate = new DateTime(update.Value.CreationDate.Year, update.Value.CreationDate.Month, update.Value.CreationDate.Day, 0, 0, 0, 0, DateTimeKind.Local);
                if (today - creationDate >= new TimeSpan(Results.ApprovalDelay, 0, 0, 0, 0)) {
                    foreach (string productFamilyTitle in update.Value.ProductFamilyTitles) {
                        if ((Results.Products.Contains(productFamilyTitle) || Results.Products.Contains("All Synchronized Products") && !Results.Products.Contains(String.Format("-{0}", productFamilyTitle))) && ((Results.TitleFilters != string.Empty && Regex.IsMatch(update.Value.Title, Results.TitleFilters, RegexOptions.IgnoreCase)) || (Results.TitleFilters == string.Empty && Regex.IsMatch(update.Value.Title, Results.TitleFilters, RegexOptions.IgnoreCase)))) {
                            visibleUpdates.Add(update.Value);
                            int creationDateLength = update.Value.CreationDate.ToLocalTime().ToString(GetDateTimeFormat()).Length;
                            int productFamilyLength = StringCollectionJoin(update.Value.ProductFamilyTitles).Length;
                            int productTitleLength = StringCollectionJoin(update.Value.ProductTitles).Length;
                            int titleLength = update.Value.Title.Length;

                            if (creationDateLength > (int)objectLengths["CreationDate"]) {
                                objectLengths["CreationDate"] = creationDateLength;
                            }
                            if (productFamilyLength > (int)objectLengths["ProductFamily"]) {
                                objectLengths["ProductFamily"] = productFamilyLength;
                            }
                            if (productTitleLength > (int)objectLengths["ProductTitle"]) {
                                objectLengths["ProductTitle"] = productTitleLength;
                            }
                            if (titleLength > (int)objectLengths["Title"]) {
                                objectLengths["Title"] = titleLength;
                            }
                        }
                    }
                }
            }
            if (visibleUpdates.Count > 0) {
                int approvedUpdates = 0;
                if (UpdateServer == null && (autoApproveUpdates == true || options.AskAnyway == true)) {
                    try {
                        Console.WriteLine("Connecting to {0} on port {1} {2}", Results.WSUSServer, Results.WSUSPort, Results.WSUSUseSSL == true ? "using SSL" : "using HTTP");
                        UpdateServer = AdminProxy.GetUpdateServer(WSUSServer, Secure, WSUSPort);
                        Console.WriteLine("Connected to " + UpdateServer.Name.ToString() + " [WSUS v." + UpdateServer.Version.ToString() + "]");
                        approvalGroup = UpdateServer.GetComputerTargetGroup(Results.GroupId);
                    } catch (Exception ex) {
                        throw ex;
                    }
                }
                Console.WriteLine();
                var headerThickness = new LineThickness(LineWidth.None, LineWidth.None, LineWidth.Single, LineWidth.Single);
                var doc = new Document(
                new Grid {
                    Stroke = LineThickness.None,
                    Color = ConsoleColor.Gray,
                    Columns = { GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto },
                    Children = {
                        new Cell("Creation Date") { Stroke = headerThickness },
                        new Cell("Product Family") { Stroke = headerThickness },
                        new Cell("Product Title") { Stroke = headerThickness },
                        new Cell("Title") { Stroke = headerThickness },
                        visibleUpdates.Select(item => new[] {
                            new Cell(item.CreationDate.ToLocalTime().ToString(GetDateTimeFormat())) { Stroke = LineThickness.None, Align = Align.Left },
                            new Cell(item.ProductFamilyTitles) { /*MaxWidth = 27,*/ Stroke = LineThickness.None, Align = Align.Left },
                            new Cell(StringCollectionJoin(item.ProductTitles, appendNewLine: true)) { /*MaxWidth = 50,*/ Stroke = LineThickness.None, Align = Align.Left },
                            new Cell(item.Title) { MinWidth = 25, Stroke = LineThickness.None, Align = Align.Left },
                        })
                    }
                });
                string renderedText = ConsoleRenderer.RenderDocumentToText(doc, new TextRenderTarget(), new Rect(0, 0, Console.WindowWidth - 1, Size.Infinity));
                string[] lines = renderedText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                Console.WriteLine(lines[0]);
                Console.WriteLine(lines[1]);
                int updateIndex = 0;
                for (var i = 2; i <= lines.Count() - 2; i++) {
                    Console.WriteLine(lines[i]);
                    if (!lines[i].StartsWith("-") && (!lines[i].StartsWith(" ") && !lines[i + 1].StartsWith(" ")) || (lines[i].StartsWith(" ") && !lines[i + 1].StartsWith(" "))) {
                        if (autoApproveUpdates == false) {
                            if (options.AskAnyway == true) {
                                Console.Write("Approve? [N]: ", updateIndex);
                                string readLine = Console.ReadLine();
                                if (readLine != String.Empty && readLine.ToUpper() == "Y") {
                                    ApproveUpdateId(UpdateServer, visibleUpdates[updateIndex].Id, approvalGroup, options.WhatIf);
                                    approvedUpdates += 1;
                                }
                            }
                        } else {
                            ApproveUpdateId(UpdateServer, visibleUpdates[updateIndex].Id, approvalGroup, options.WhatIf);
                            approvedUpdates += 1;
                        }
                        updateIndex += 1;
                    } else {
                        continue;
                    }
                }
                if (options.WhatIf != true && approvedUpdates > 0) {
                    Properties.Settings.Default.PreviousScan = String.Empty;
                    Properties.Settings.Default.Save();
                }
                Console.WriteLine("Total Updates: {0}, Visible Updates: {1}{2}", orderedUpdates.Count(), visibleUpdates.Count, approvedUpdates > 0 ? options.WhatIf == false ? String.Format(", Approved Updates: {0}", approvedUpdates) : String.Format(", WhatIf Updates: {0}", approvedUpdates) : "");
            } else {
                Console.WriteLine("Total Updates: {0}, Visible Updates: {1}", orderedUpdates.Count(), visibleUpdates.Count);
            }
        }
        private static string GetTitleFilters(bool displayHeader, string previousResults = "") {
            if (displayHeader == true) {
                Console.WriteLine("-----------------------------------------------------------------------------------");
                Console.WriteLine("| Title Filter(s)                                                                 |");
                Console.WriteLine("-----------------------------------------------------------------------------------");
            }
            Console.Write("Would you like to {0} Title Filters? (Y or 1|N or 0) [N]: ", previousResults == "" ? "specify any" : "change the");
            string inputFilter = Console.ReadLine();
            string titleFilter = String.Empty;
            if (inputFilter != String.Empty && (inputFilter.ToUpper() == "Y" || inputFilter == "1")) {
                Console.Write("Title Filter(s): ");
                titleFilter = Console.ReadLine();
            } else {
                titleFilter = previousResults;
            }
            return titleFilter;
        }
        private static int GetApprovalDelay() {
            Console.Write("Please specify the minimum number of days or weeks after patch release date to approve [0d]: ");
            string input = Console.ReadLine();
            if (input != String.Empty) {
                if (input.EndsWith("d") || input.EndsWith("w")) {
                    if (input.EndsWith("w")) {
                        if (int.TryParse(input.Replace("w", ""), out int approvalWeeks)) {
                            return approvalWeeks * 7;
                        }
                    }
                    if (input.EndsWith("d")) {
                        if (int.TryParse(input.Replace("d", ""), out int approvalDays)) {
                            return approvalDays;
                        }
                    }
                }
            } else {
                return 0;
            }
            return -1;
        }
        private static void DoSomeWork(CommandLineOptions opts) {
            //Console.WriteLine("Command Line parameters provided were valid :)");
        }
        private static void HandleParseError(IEnumerable errs) {
            //Console.WriteLine("Command Line parameters provided were not valid!");
            Environment.Exit(0);
        }
        private static void ApproveUpdateId(IUpdateServer updateServer, Guid guid, IComputerTargetGroup targetGroup, bool whatIf) {
            try {
                IUpdate update = updateServer.GetUpdate(new UpdateRevisionId { UpdateId = guid });
                if (whatIf == true) {
                    Console.WriteLine("WhatIf: Approving Update - {0}...", update.Title);
                } else {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Approving Update - {0}...", update.Title);
                    Console.ResetColor();
                    if (update.RequiresLicenseAgreementAcceptance == true) {
                        update.AcceptLicenseAgreement();
                    }
                    update.Approve(UpdateApprovalAction.Install, targetGroup);
                }
            } catch (Exception e) {
                Console.WriteLine("Error: {0}", e.Message);
            }

        }
        private static List<UpdateModel> GetUpdateInfo(IUpdateServer updateServer, IComputerTarget computer) {
            List<UpdateModel> updates = new List<UpdateModel>();
            UpdateInstallationInfoCollection neededAndNotInstalled = updateServer.GetComputerTargetByName(computer.FullDomainName).GetUpdateInstallationInfoPerUpdate();

            if (neededAndNotInstalled != null) {
                foreach (IUpdateInstallationInfo item in neededAndNotInstalled) {
                    if (item.UpdateInstallationState == UpdateInstallationState.NotInstalled && item.UpdateApprovalAction == UpdateApprovalAction.NotApproved) {
                        IUpdate updateInfo = updateServer.GetUpdate(new UpdateRevisionId { UpdateId = item.UpdateId });
                        UpdateModel update = new UpdateModel();
                        update.Id = item.UpdateId;
                        update.Title = updateInfo.Title;
                        update.CreationDate = updateInfo.CreationDate;
                        update.ProductFamilyTitles = updateInfo.ProductFamilyTitles;
                        update.ProductTitles = updateInfo.ProductTitles;
                        updates.Add(update);
                    }
                }
            }
            return updates;
        }
        private static StringCollection GetProducts() {
            StringCollection productFamilies = Properties.Settings.Default.ProductFamilies;
            StringCollection choices = new StringCollection();
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.WriteLine("| Product Families                                                                |");
            Console.WriteLine("-----------------------------------------------------------------------------------");
            foreach (string ProductFamily in productFamilies) {
                int currentItem = productFamilies.IndexOf(ProductFamily);
                Console.WriteLine("{0} - {1}", currentItem, productFamilies[currentItem]);
            }
            Console.WriteLine("-----------------------------------------------------------------------------------");
            Console.Write("Select which Product Family or Families to update: [0 - {0}] [0]: ", productFamilies.Count - 1);
            string input = Console.ReadLine();
            if (input != String.Empty) {
                if (input.Contains(",")) {
                    string[] items = input.Split(',');
                    foreach (string item in items) {
                        string trim = item.Trim();
                        if (trim.StartsWith("-")) {
                            if (int.TryParse(trim.Substring(1), out int result)) {
                                if (result != 0) {
                                    choices.Add(String.Format("-{0}", productFamilies[result]));
                                }
                            }
                        } else {
                            if (int.TryParse(item, out int result)) {
                                choices.Add(productFamilies[result]);
                            }
                        }
                    }
                } else {
                    if (int.TryParse(input, out int result)) {
                        if (result != 0) {
                            choices.Add(productFamilies[result]);
                        } else {
                            choices.Add(productFamilies[0]);
                        }
                    } else {
                        choices.Add(productFamilies[0]);
                    }
                }
            } else {
                choices.Add(productFamilies[0]);
            }
            return choices;
        }
        private static String GetDateTimeFormat() {
            CultureInfo currentCulture = CultureInfo.CurrentCulture;
            string dateFormat = currentCulture.DateTimeFormat.ShortDatePattern;
            string timeFormat = currentCulture.DateTimeFormat.LongTimePattern;

            if (dateFormat.Contains("M") && !dateFormat.Contains("MM") && !dateFormat.Contains("MMM")) {
                dateFormat = dateFormat.Replace(String.Format("M{0}", currentCulture.DateTimeFormat.DateSeparator), String.Format("MM{0}", currentCulture.DateTimeFormat.DateSeparator));
            }

            if (dateFormat.Contains("d") && !dateFormat.Contains("dd")) {
                if (dateFormat.Contains(String.Format("d{0}", currentCulture.DateTimeFormat.DateSeparator))) {
                    dateFormat = dateFormat.Replace(String.Format("d{0}", currentCulture.DateTimeFormat.DateSeparator), String.Format("dd{0}", currentCulture.DateTimeFormat.DateSeparator));
                } else if (dateFormat.Contains(String.Format("{0}d", currentCulture.DateTimeFormat.DateSeparator))) {
                    dateFormat = dateFormat.Replace(String.Format("{0}d", currentCulture.DateTimeFormat.DateSeparator), String.Format("{0}dd", currentCulture.DateTimeFormat.DateSeparator));
                }
            }

            if (timeFormat.Contains("h") && !timeFormat.Contains("hh")) {
                timeFormat = timeFormat.Replace(String.Format("h{0}", currentCulture.DateTimeFormat.TimeSeparator), String.Format("hh{0}", currentCulture.DateTimeFormat.TimeSeparator));
            }
            if (timeFormat.Contains("H") && !timeFormat.Contains("HH")) {
                timeFormat = timeFormat.Replace(String.Format("H{0}", currentCulture.DateTimeFormat.TimeSeparator), String.Format("HH{0}", currentCulture.DateTimeFormat.TimeSeparator));
            }
            if (timeFormat.Contains("m") && !timeFormat.Contains("mm")) {
                timeFormat = timeFormat.Replace(String.Format("m{0}", currentCulture.DateTimeFormat.TimeSeparator), String.Format("mm{0}", currentCulture.DateTimeFormat.TimeSeparator));
            }
            return String.Format("{0} {1}", dateFormat, timeFormat);
        }
        private static string ConvertApprovalDelay(int delay, bool valueOnly = false) {
            string result = string.Empty;
            if (delay != 0 && delay % 7 == 0) {
                if (delay / 7 > 1) {
                    result = String.Format("(RD + {0} weeks)", delay / 7);
                } else {
                    result = "(RD + 1 week)";
                }
            } else {
                if (delay >= 1) {
                    if (delay > 1) {
                        result = String.Format("(RD + {0} days)", delay);
                    } else {
                        result = "(RD + 1 day)";
                    }
                } else if (delay == 0) {
                    result = "(Release Date)";
                } else {
                    result = "(Manual)";
                }
            }
            return result;
        }
        private static string StringCollectionJoin(StringCollection collection, bool appendNewLine = false/*, int characterLimit*/) {
            StringBuilder sb = new StringBuilder(/*characterLimit*/);
            foreach (string s in collection) {
                if (sb.Length > 0)
                    if (appendNewLine == true) {
                        sb.Append(",\r\n");
                    } else {
                        sb.Append(", ");
                    }
                sb.Append(s);
            }
            return sb.ToString();
        }
        private static string SerializeResultsObject(object o) {
            if (!o.GetType().IsSerializable) {
                return null;
            }

            using (MemoryStream stream = new MemoryStream()) {
                new BinaryFormatter().Serialize(stream, o);
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static object DeserializeResultsObject(string str) {
            byte[] bytes = Convert.FromBase64String(str);

            using (MemoryStream stream = new MemoryStream(bytes)) {
                return new BinaryFormatter().Deserialize(stream);
            }
        }
        private static int LookupApprovalDelay(string group, Dictionary<int, int> rings) {
            if (int.TryParse(group, out int ringCheck)) {
                if (rings.ContainsKey(ringCheck)) {
                    return rings[ringCheck];
                } else {
                    return -1;
                }
            } else {
                return -1;
            }
        }
    }
}
