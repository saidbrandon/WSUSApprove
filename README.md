WSUSApprove
===========
Console Application to approve WSUS updates written in C#.

Problems fixed by WSUSApprove:
- Never use the WSUS MMC to approve updates again
- Only approve updates that are older than x days or weeks
- Approve or ignore updates for specific Product Families i.e. "SQL Server" or "Exchange"
- Filter updates by title
- Approve only updates needed by computers in a group
- Auto approve all needed updates or individually via Y/N

## Prerequisites
- Run WSUSApprove from your top level WSUS Server (recommended) or a Desktop/Server with RSAT Tools installed.
- .NET Framework 4.8

## Installation
Extract release file to a folder of your choosing i.e. C:\Program Files\WSUSApprove

## Configuration
Edit the WSUSApprove.exe.config file to configure your desired settings. Listed below are the default values.   
**TODO:** Make WSUS Server settings accessible via command-line and Groups/Rings configurable without having to edit the file directly.

**WSUS Server**
```xml
<setting name="WSUSServer" serializeAs="String">
    <value>localhost</value>
</setting>
<setting name="WSUSPort" serializeAs="String">
    <value>8530</value>
</setting>
<setting name="WSUSUseSSL" serializeAs="String">
    <value>False</value>
</setting>
```
**Approval Rings**
```xml
<ApprovalRings>
    <add key="0" value="0"/>
    <add key="1" value="7"/>
    <add key="2" value="14"/>
    <add key="3" value="21"/>
</ApprovalRings>
```
Approval Rings specify the number of days required to elapase before presenting an update as available for approval. A value of -1 is considered "Manual" and you will be asked to specify the number of days during execution. You can also override the default values during execution. See Command Line Arguments for more information. The key is used to to associate the specified days with a WSUS group listed in Groups.

**Groups**
```xml
<Groups>
    <add key="All Computers" value="-1"/>
    <add key="Ring 0 - Desktops" value="0"/>
    <add key="Ring 1 - Servers - Non-Production" value="1"/>
    <add key="Ring 2 - Servers - Production" value="2"/>
    <add key="Ring 2 - Servers - Production - Delayed" value="3"/>
</Groups>
```
Associate your WSUS groups with a default approval delay. The key is your WSUS group name and the value is the ApprovalRing key.   
If the groups defined here don't exist, they won't be visible during execution. Additionally, if you have other groups that aren't listed, they won't be visible either. For example, if you ignore this setting and run the application you will only see the following output:
```text
-----------------------------------------------------------------------------------
| Groups                                                                          |
-----------------------------------------------------------------------------------
0 - All Computers                              (Manual)                 xyz members
-----------------------------------------------------------------------------------
Select which Group to update: [0 - 0] [0]:
```
See Command Line Arguments if you want to override this.
## Usage
```console
C:\Program Files\WSUSApprove>WSUSApprove.exe
Connecting to localhost on port 8530 using HTTP
Connected to localhost [WSUS v.10.0.14393.2969]
Gathering Group Information, Please Wait...
-----------------------------------------------------------------------------------
| Product Families                                                                |
-----------------------------------------------------------------------------------
0 - All Synchronized Products
1 - Windows
2 - Forefront
3 - Office
4 - SQL Server
5 - Exchange
6 - Silverlight
7 - Developer Tools, Runtimes, and Redistributables
-----------------------------------------------------------------------------------
Select which Product Family or Families to update: [0 - 7] [0]: 0,-4
-----------------------------------------------------------------------------------
| Title Filter(s)                                                                 |
-----------------------------------------------------------------------------------
Would you like to specify any Title Filters? (Y or 1|N or 0) [N]:
-----------------------------------------------------------------------------------
| Groups                                                                          |
-----------------------------------------------------------------------------------
0 - All Computers                              (Manual)                 xyz members
-----------------------------------------------------------------------------------
Select which Group to update: [0 - 0] [0]: 0
-----------------------------------------------------------------------------------
| Invalid / Unknown / Manual Ring                                                 |
-----------------------------------------------------------------------------------
Please specify the minimum number of days or weeks after patch release date to approve [0d]:
Updates released on or before 1/7/2022 will qualify for approval
Would you like to Auto Approve Updates? (Y or 1|N or 0) [N]:

Creation Date          Product Family Product Title                           Title
---------------------- -------------- --------------------------------------- ----------------------------------------------------------------------------------------------------
12/14/2021 12:00:06 PM Windows        Windows Server 2012 R2                  2021-12 Security Monthly Quality Rollup for Windows Server 2012 R2 for x64-based Systems (KB5008263)
12/14/2021 12:00:06 PM Windows        Windows Server 2016                     2021-12 Cumulative Update for Windows Server 2016 for x64-based Systems (KB5008207)
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB5002104) 32-Bit Edition
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Excel 2013 (KB5002105) 32-Bit Edition
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB5002101) 32-Bit Edition
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB4486726) 32-Bit Edition
12/14/2021 10:30:00 AM Windows        Windows Server 2016,                    Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)
                                      Windows 8.1,
                                      Windows Server 2012,
                                      Windows 10,
                                      Windows 10 LTSB,
                                      Windows Server 2012 R2,
                                      Windows Server 2019,
                                      Windows 10, version 1903 and later,
                                      Windows Server, version 1903 and later,
                                      Windows 11
12/14/2021 10:30:00 AM Windows        Windows Server 2008,                    Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)
                                      Windows 7,
                                      Windows Server 2008 R2
12/14/2021 10:30:00 AM Windows        Windows Server 2008,                    Windows Malicious Software Removal Tool - v5.96 (KB890830)
                                      Windows 7
Total Updates: 9, Visible Updates: 9

C:\Program Files\WSUSApprove>WSUSApprove.exe --usepreviousscan
-----------------------------------------------------------------------------------
| Previous Scan Detected                                                          |
-----------------------------------------------------------------------------------
Server:          http://localhost:8530
Date:            1/7/2022 7:40:43 AM
Product(s):      All Synchronized Products, -SQL Server
Title Filter(s):
Group:           All Computers
Approval Delay:  0 day(s)
-----------------------------------------------------------------------------------
Re-Use Results? (Y or 1|N or 0) [N]: y
Would you like to change the Product Selection? (Y or 1|N or 0) [N]:
Would you like to specify any Title Filters? (Y or 1|N or 0) [N]:
Would you like to change the Approval Delay? (Y or 1|N or 0) [N]:
Would you like to Auto Approve Updates? (Y or 1|N or 0) [N]: y
Connecting to localhost on port 8530 using HTTP
Connected to localhost [WSUS v.10.0.14393.2969]

Creation Date          Product Family Product Title                           Title
---------------------- -------------- --------------------------------------- ----------------------------------------------------------------------------------------------------
12/14/2021 12:00:06 PM Windows        Windows Server 2012 R2                  2021-12 Security Monthly Quality Rollup for Windows Server 2012 R2 for x64-based Systems (KB5008263)
Approving Update - 2021-12 Security Monthly Quality Rollup for Windows Server 2012 R2 for x64-based Systems (KB5008263)...
12/14/2021 12:00:06 PM Windows        Windows Server 2016                     2021-12 Cumulative Update for Windows Server 2016 for x64-based Systems (KB5008207)
Approving Update - 2021-12 Cumulative Update for Windows Server 2016 for x64-based Systems (KB5008207)...
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB5002104) 32-Bit Edition
Approving Update - Security Update for Microsoft Office 2013 (KB5002104) 32-Bit Edition...
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Excel 2013 (KB5002105) 32-Bit Edition
Approving Update - Security Update for Microsoft Excel 2013 (KB5002105) 32-Bit Edition...
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB5002101) 32-Bit Edition
Approving Update - Security Update for Microsoft Office 2013 (KB5002101) 32-Bit Edition...
12/14/2021 12:00:00 PM Office         Office 2013                             Security Update for Microsoft Office 2013 (KB4486726) 32-Bit Edition
Approving Update - Security Update for Microsoft Office 2013 (KB4486726) 32-Bit Edition...
12/14/2021 10:30:00 AM Windows        Windows Server 2016,                    Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)
                                      Windows 8.1,
                                      Windows Server 2012,
                                      Windows 10,
                                      Windows 10 LTSB,
                                      Windows Server 2012 R2,
                                      Windows Server 2019,
                                      Windows 10, version 1903 and later,
                                      Windows Server, version 1903 and later,
                                      Windows 11
Approving Update - Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)...
12/14/2021 10:30:00 AM Windows        Windows Server 2008,                    Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)
                                      Windows 7,
                                      Windows Server 2008 R2
Approving Update - Windows Malicious Software Removal Tool x64 - v5.96 (KB890830)...
12/14/2021 10:30:00 AM Windows        Windows Server 2008,                    Windows Malicious Software Removal Tool - v5.96 (KB890830)
                                      Windows 7
Approving Update - Windows Malicious Software Removal Tool - v5.96 (KB890830)...
Total Updates: 9, Visible Updates: 9, Approved Updates: 9
```
** Approving Update - Update Title ** is highlighted in yellow for visibility and easy reading, but not within the Readme.

## Command Line Arguments
```text
--ShowAllGroups
    Displays All WSUS Groups instead of just the predefined groups.

--AskAnyway
    If specified, will ask to approve updates when auto approving updates is set to no. This parameter will prompt for approval on every needed update detected instead of approving none, or all updates.

--ApprovalOverride
    If specified, you will be prompted to enter the approval delay in days or weeks regardless if it's previously defined or not.

--UsePreviousScan
    If specified and detected, script will not rescan WSUS Server for needed updates. This is useful if the first run is used to only display needed updates, then run again to approve updates.

--WhatIf
    If specified, and you approve an update, the update will display information as if it was approved, but doesn't actually approve.
```

## Additional Licenses
[CommandLineParser](https://github.com/commandlineparser/commandline) - https://github.com/commandlineparser/commandline/blob/master/License.md   
[CsConsoleFormat](https://github.com/Athari/CsConsoleFormat) - https://github.com/Athari/CsConsoleFormat/blob/master/License.md

