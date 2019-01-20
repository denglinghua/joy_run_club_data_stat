﻿using System;
using System.Collections.Generic;
using System.IO;

using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace RunData
{
    class DataSource
    {
        public DateRange CurrentDateRange;
        public string Group;
        public List<RunRecord> RunRecoreds;
        public NonBreakData NoRunData;
        private MemberData memberData;
        public NonBreakData NonBreakRunData;
        private List<long> LeaveMemberIdList;

        public static readonly DataSource Instance = new DataSource();

        private static readonly string MEMBER_DATA_FILE = "data_members";
        private static readonly string NO_RUN_DATA_FILE = "data_no_run";
        private static readonly string NON_BREAK_RUN_DATA_FILE = "data_no_break_run";

        public static void Init(string runRecordFile, string[] noRunFiles, string leaveFile)
        {
            // 所有的load数据方法，都是只加载原始数据，不做任何处理，千万不要边加载，边处理。
            // 处理都在所有加载之后，这样比较好协调处理顺序。顺序！顺序！顺序！非常重要！
            Instance.LoadRunRecord(runRecordFile);

            Instance.LoadNoRunData(noRunFiles);

            Instance.LoadMemberData(MEMBER_DATA_FILE);

            Instance.LoadLeaveData(leaveFile);

            Instance.LoadPreviousNoRunData(NO_RUN_DATA_FILE);

            Instance.LoadPreviousNonBreakRunData(NON_BREAK_RUN_DATA_FILE);
        }

        public void HandleData()
        {
            this.AddNewMembers();

            this.ClassifyRunData();

            this.MarkLeaveForNoRunData();

            this.MarkNewMemberForNoRunData();

            this.NoRunData.Merge();
            this.NonBreakRunData.Merge();
        }

        private void AddNewMembers()
        {
            foreach (RunRecord r in this.RunRecoreds)
            {
                this.memberData.TryAdd(r.Member);
            }

            foreach (NonBreakRecord r in this.NoRunData.GetCurrentData())
            {
                this.memberData.TryAdd(r.Member);
            }
        }

        // 请假了相当于跑了，所以要从无跑步人员中移除，避免不达标上榜
        private void MarkLeaveForNoRunData()
        {
            Logger.Info("在当周不达标人员中标注请假");

            if (this.LeaveMemberIdList.Count == 0) return;

            List<NonBreakRecord> l = this.NoRunData.GetCurrentData();

            foreach (NonBreakRecord r in l.ToArray())
            {
                if (this.LeaveMemberIdList.Contains(r.Member.JoyRunId))
                {
                    l.Remove(r);
                    Logger.Info("    {0}", r.Member);
                }
            }
        }

        private void MarkNewMemberForNoRunData()
        {
            Logger.Info("在当周不达标人员中标注新加入成员");

            List<NonBreakRecord> l = this.NoRunData.GetCurrentData();

            bool isNew;
            foreach (NonBreakRecord r in l.ToArray())
            {
                isNew = this.memberData.isNewMember(r.Member);
                if (isNew)
                {
                    l.Remove(r);
                    Logger.Info("    {0}", r.Member);
                }
            }
        }

        private void ClassifyRunData()
        {
            Logger.Info("分离跑步达标和不达标的数据");

            foreach (RunRecord r in this.RunRecoreds)
            {
                string reason = null;

                if (!r.IsQualifiedOfDistance)
                {
                    reason = string.Format("跑量：{0}", r.Distance);
                }

                if (!r.IsQualifiedOfAvgPace)
                {
                    reason = string.Format("配速：{0}", RunRecord.ToTimeSpanFromSeconds(r.AvgPaceSeconds));
                }


                if (reason != null)
                {
                    this.NoRunData.AddCurrentRecord(r.Member, reason);
                }
                else
                {
                    this.NonBreakRunData.AddCurrentRecord(r.Member, string.Empty);
                }
            }
        }

        private void LoadRunRecord(string fileName)
        {
            Logger.Info("加载跑步统计数据");

            IWorkbook book = null;
            ISheet sheet = null;

            using (FileStream FS = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                book = WorkbookFactory.Create(FS);
                sheet = book.GetSheetAt(0);

                this.Group = GetCellByReference(sheet, "B1").StringCellValue;

                ICell dataRangeCell = GetCellByReference(sheet, "B3");
                String dataRangeStr = dataRangeCell.StringCellValue;
                this.CurrentDateRange = DateRange.Create(dataRangeStr.Split(new String[] { "--" }, StringSplitOptions.None), "yyyy-MM-dd HH:mm:ss");

                this.RunRecoreds = new List<RunRecord>();
                for (int rowIndex = 10; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow row = sheet.GetRow(rowIndex);

                    //用户昵称  用户ID  所属跑团名称  性别  总跑量（公里）  总用时  跑步次数
                    string[] values = ReadRowToArray(row, 7);

                    this.RunRecoreds.Add(
                        new RunRecord(long.Parse(values[1]), values[0], values[3], values[2], float.Parse(values[4]), TimeSpan.Parse(values[5]).TotalSeconds,
                        short.Parse(values[6])));
                }

                Logger.Info("    数据时段：{0} ，跑步记录 {1} 条", this.CurrentDateRange.ToString(), this.RunRecoreds.Count);
            }
        }

        private void LoadNoRunData(string[] noRunFiles)
        {
            Logger.Info("加载无跑步成员数据");

            this.NoRunData = new NonBreakData("不达标", CurrentDateRange.Start);
            foreach (string fileName in noRunFiles)
            {
                LoadOneFileNoRunMembers(fileName);
            }
        }

        private void LoadOneFileNoRunMembers(string fileName)
        {
            IWorkbook book = null;
            ISheet sheet = null;

            using (FileStream FS = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                book = WorkbookFactory.Create(FS);
                sheet = book.GetSheetAt(0);

                ICell groupCell = GetCellByReference(sheet, "B4");
                String groupName = groupCell.StringCellValue;

                int c = 0;
                for (int rowIndex = 6; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow row = sheet.GetRow(rowIndex);

                    //悦跑ID 昵称 性别 总跑量（公里） 最后跑步时间
                    string[] values = ReadRowToArray(row, 3);

                    this.NoRunData.AddCurrentRecord(new Member(long.Parse(values[0]), values[1], values[2], groupName), "没跑步");

                    c++;
                }

                Logger.Info("    {0} ：{1} 人未跑步", groupName, c);
            }
        }

        private void LoadMemberData(string fileName)
        {
            this.memberData = new MemberData();

            this.memberData.LoadPreviousData(fileName);
        }

        private void LoadLeaveData(string fileName)
        {
            this.LeaveMemberIdList = new List<long>();

            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            Logger.Info("加载请假数据");

            IWorkbook book = null;
            ISheet sheet = null;

            using (FileStream FS = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                book = WorkbookFactory.Create(FS);
                sheet = book.GetSheetAt(0);

                for (int rowIndex = 3; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow row = sheet.GetRow(rowIndex);

                    //报名时间 昵称 悦跑圈ID号  请假原因
                    string[] values = ReadRowToArray(row, 4);

                    this.LeaveMemberIdList.Add(long.Parse(values[2]));
                }

                Logger.Info("    {0} 人请假", this.LeaveMemberIdList.Count);
            }
        }

        private void LoadPreviousNoRunData(string fileName)
        {
            this.NoRunData.LoadPreviousData(fileName);
        }

        private void LoadPreviousNonBreakRunData(string fileName)
        {
            this.NonBreakRunData = new NonBreakData("达标", this.CurrentDateRange.Start);
            this.NonBreakRunData.LoadPreviousData(fileName);
        }

        private static ICell GetCellByReference(ISheet sheet, string reference)
        {
            CellReference cr = new CellReference(reference);
            IRow row = sheet.GetRow(cr.Row);
            return row.GetCell(cr.Col);
        }

        // 都以string读入是防止源头excel不按格式写数据，不管3721都先转string
        private static string[] ReadRowToArray(IRow row, int columnCount)
        {
            string[] values = new string[columnCount];
            for (int cellIndex = 0; cellIndex < values.Length; cellIndex++)
            {
                ICell cell = row.GetCell(cellIndex);
                string valueStr = string.Empty;
                switch (cell.CellType)
                {
                    case CellType.String:
                        valueStr = cell.StringCellValue;
                        break;
                    case CellType.Numeric:
                        valueStr = cell.NumericCellValue.ToString();
                        break;
                }
                values[cellIndex] = valueStr;
            }

            return values;
        }

        public void Save()
        {
            this.memberData.Save(MEMBER_DATA_FILE);
            this.NoRunData.Save(NO_RUN_DATA_FILE);
            this.NonBreakRunData.Save(NON_BREAK_RUN_DATA_FILE);
        }
    }
}
