﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace RunData
{
    class DataSource
    {
        public DateRange DateRange;
        public string Group;
        public List<RunRecord> RunRecoreds;
        public NoRunData NoRunData;
        public NonBreakRunData NonBreakRunData;
        private List<long> LeaveMemberIdList;        

        public static readonly DataSource Instance = new DataSource();

        public static readonly string NO_RUN_DATA_FILE = "no_run_data.txt";
        public static readonly string NON_BREAK_RUN_DATA_FILE = "no_break_run_data.txt";        

        public static void Init(string runRecordFile, string[] noRunFiles, string leaveFile)
        {
            Instance.LoadRunRecord(runRecordFile);

            Instance.LoadNoRunData(noRunFiles);

            Instance.LoadLeaveData(leaveFile);

            Instance.LoadPreviousNoRunData(NO_RUN_DATA_FILE);
        }

        public void HandleData()
        {
            this.PickNoUnQualifiedRunData();

            this.NoRunData.HandleData(this.LeaveMemberIdList);
        }        

        private void PickNoUnQualifiedRunData()
        {
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
                    this.NoRunData.AddCurrentNoRunRecord(r.Member, reason, DateRange);
                }
            }
        }        

        private void LoadRunRecord(string fileName)
        {
            IWorkbook book = null;
            ISheet sheet = null;

            using (FileStream FS = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                book = WorkbookFactory.Create(FS);
                sheet = book.GetSheetAt(0);

                this.Group = GetCellByReference(sheet, "B1").StringCellValue;

                ICell dataRangeCell = GetCellByReference(sheet, "B3");
                String dataRangeStr = dataRangeCell.StringCellValue;
                this.DateRange = DateRange.Create(dataRangeStr.Split(new String[] { "--" }, StringSplitOptions.None), "yyyy-MM-dd HH:mm:ss");

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
            }
        }

        private void LoadNoRunData(string[] noRunFiles)
        {
            this.NoRunData = new NoRunData();
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

                for (int rowIndex = 6; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow row = sheet.GetRow(rowIndex);

                    //悦跑ID 昵称 性别 总跑量（公里） 最后跑步时间
                    string[] values = ReadRowToArray(row, 3);

                    this.NoRunData.AddCurrentNoRunRecord(new Member(long.Parse(values[0]), values[1], values[2], groupName), "没跑步", DateRange);
                }
            }
        }

        private void LoadLeaveData(string fileName)
        {
            IWorkbook book = null;
            ISheet sheet = null;

            using (FileStream FS = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                book = WorkbookFactory.Create(FS);
                sheet = book.GetSheetAt(0);

                this.LeaveMemberIdList = new List<long>();
                for (int rowIndex = 3; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow row = sheet.GetRow(rowIndex);

                    //报名时间 昵称 悦跑圈ID号  请假原因
                    string[] values = ReadRowToArray(row, 4);

                    this.LeaveMemberIdList.Add(long.Parse(values[2]));
                }
            }
        }

        private void LoadPreviousNoRunData(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return;
            }

            string[] lines = File.ReadAllLines(fileName);
            foreach (string s in lines)
            {
                //88474417	Samryi	男	广·马帮_神马分队	20190107-20190113
                string[] a = s.Split('\t');
                this.NoRunData.AddPreviousNoRunRecord(new Member(long.Parse(a[0]), a[1], a[2], a[3]),
                    a[4].Split(','));
            }
        }

        private void LoadPreviousNonBreakRunData(string fileName)
        {
            this.NonBreakRunData = new NonBreakRunData();

            if (!File.Exists(fileName))
            {
                return;
            }

            string[] lines = File.ReadAllLines(fileName);
            foreach (string s in lines)
            {
                //88474417	Samryi	男	广·马帮_神马分队	20190107-20190113
                string[] a = s.Split('\t');
                this.NonBreakRunData.AddPreviousRunRecord(new Member(long.Parse(a[0]), a[1], a[2], a[3]),
                    a[4].Split(','));
            }
        }

        private static ICell GetCellByReference(ISheet sheet, string reference)
        {
            CellReference cr = new CellReference(reference);
            IRow row = sheet.GetRow(cr.Row);
            return row.GetCell(cr.Col);
        }

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
    }
}
