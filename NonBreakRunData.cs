﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RunData
{
    class NonBreakRunData
    {
        private Dictionary<Member, NonBreakRunRecord> previousRunList = new Dictionary<Member, NonBreakRunRecord>();
        private List<NonBreakRunRecord> runList = new List<NonBreakRunRecord>();

        public void AddPreviousRunRecord(Member member, string[] times)
        {
            this.previousRunList.Add(member, new NonBreakRunRecord(member, times));
        }

        public void AddCurrentNoRunRecord(Member member, DateRange dateRange)
        {
            this.runList.Add(new NonBreakRunRecord(member, dateRange));
        }

        public void HandleData()
        {
            this.Merge();
        }

        private void Merge()
        {
            foreach (NonBreakRunRecord rec in this.runList)
            {
                if (previousRunList.ContainsKey(rec.Member))
                {
                    rec.AddTime(previousRunList[rec.Member].GetTimes());
                }
            }
        }

        public void SavePreviousNoBreakRunData()
        {
            List<string> lines = new List<string>();

            foreach (NonBreakRunRecord nr in this.runList)
            {
                lines.Add(String.Format("{0}\t{1}", nr.Member, string.Join(",", Keep3Times(nr.GetTimes()))));
            }


            File.WriteAllText(DataSource.NON_BREAK_RUN_DATA_FILE, string.Join(Environment.NewLine, lines.ToArray()));
        }

        public static string[] Keep3Times(string[] times)
        {
            List<string> tl = new List<string>(times);
            tl.Sort();
            while (tl.Count > 3)
            {
                tl.RemoveAt(0);
            }
            return tl.ToArray();
        }
    }
}
