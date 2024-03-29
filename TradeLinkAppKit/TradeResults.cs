﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using TradeLink.Common;
using System.Reflection;
using TradeLink.API;
using System.IO;
using System.Windows.Forms;

namespace TradeLink.AppKit
{
    public partial class TradeResults : UserControl
    {
        DataTable dt = new DataTable("results");
        DataGridView dg = new DataGridView();
        PositionTracker pt = new PositionTracker(100);
        FileSystemWatcher fw;
        public const string RESULTS_POSTFIX = "Trades.csv";

        string _watchpath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        /// <summary>
        /// change path where new files are watched
        /// </summary>
        public string Path { get { return _watchpath; } set { _watchpath = value; } }


        public TradeResults()
        {
            InitializeComponent();
            dt.Columns.Add("Statistic");
            dt.Columns.Add("Result");
            //dg.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle();
            dg.ColumnHeadersDefaultCellStyle.BackColor = BackColor;
            dg.RowHeadersVisible = false;
            dg.DataSource = dt;
            dg.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dg.Parent = splitContainer1.Panel2;
            dg.Dock = DockStyle.Fill;
            dg.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            ContextMenu = new ContextMenu();
            ContextMenu.MenuItems.Add("RiskFreeRate", new EventHandler(changerfr));
            ContextMenu.MenuItems.Add("Comission", new EventHandler(changecomm));
            dg.ReadOnly = true;
            dg.BackColor = Color.White;
            dg.AutoGenerateColumns = true;
            dg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dg.ColumnHeadersHeight = (int)(dg.ColumnHeadersDefaultCellStyle.Font.Height*1.5);
            dg.ColumnHeadersDefaultCellStyle.BackColor = dg.BackColor;
            dg.ColumnHeadersDefaultCellStyle.ForeColor = dg.ForeColor;
            dg.BackgroundColor = Color.White;
            dg.Font = new Font(FontFamily.GenericSansSerif, 10);
            WatchPath();
            BackColor = Color.White;
            splitContainer1.Panel2.BackColor = Color.White;
            allresults.SelectedIndexChanged += new EventHandler(tradefiles_SelectedIndexChanged);
            Text = "Tattle " + Util.TLVersion();
            refreshgrid();
            MouseEnter += new EventHandler(TattleMain_MouseEnter);

            Resize += new EventHandler(TradeResults_Resize);

        }

        void TradeResults_Resize(object sender, EventArgs e)
        {
            if (SplitterDistance > 0)
                _lastsplit = splitContainer1.SplitterDistance;
        }



        public int SplitterDistance { get { return splitContainer1.SplitterDistance; } set { splitContainer1.SplitterDistance = value; } }

        int _lastsplit = 50;
        public void ToggleResultPicker()
        {
            if (InvokeRequired)
                Invoke(new VoidDelegate(ToggleResultPicker));
            else
            {
                if (SplitterDistance == 0)
                    SplitterDistance = _lastsplit;
                else
                    SplitterDistance = 0;
                Invalidate(true);
            }
        }

        static long getts(Trade t)
        {
            return Util.ToTLDateTime(Util.ToDateTime(t.xdate, t.xtime));
        }

        public static List<Trade> SortTrades(List<Trade> trades)
        {
            // convert to index
            var tmpxs = trades.ToArray();
            List<long> timestamps = new List<long>();
            trades.ForEach(t => timestamps.Add(getts(t)));
            var tmpts = timestamps.ToArray();
            // sort
            Array.Sort(tmpts, tmpxs);
            // convert back
            return new List<Trade>(tmpxs);
        }

        public static decimal GetPortfolioPlot(string title,decimal start, int startdate, int starttime, int enddate, int endtime, List<Trade> trades, ref ChartControl c, decimal compershare)
        {
            var cureq = start;
            if (trades.Count == 0)
                return GetPortfolioPlot(title,start, startdate, starttime, enddate, endtime, ref c);
            c.NewBarList(new BarListImpl(title));
            var tradessorted = SortTrades(trades);
            c.newPoint(title, cureq, 0, tradessorted[0].xdate, 100);
            // plot money made
            PositionTracker pt = new PositionTracker();
            foreach (var t in tradessorted)
            {
                var grosspl = pt.Adjust(t);
                var netpl = grosspl - (compershare * Math.Abs(t.xsize));
                cureq += netpl;
                c.newPoint(title, cureq, t.xtime, t.xdate, 100);
            }
            c.redraw();
            // set final equity
            return cureq;
        }

        private static decimal GetPortfolioPlot(string title, decimal staticequity, int startdate, int starttime, int enddate, int endtime, ref ChartControl c)
        {
            var cureq = staticequity;
            c.NewBarList(new BarListImpl(title));
            // plot static line
            c.newPoint(title, cureq, starttime, startdate, 100);
            c.newPoint(title, cureq, endtime, enddate, 100);
            c.redraw();
            // set final equity
            return cureq;
        }

        decimal _rfr = .01m;
        decimal _comm = .01m;
        void changerfr(object sender, EventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("Risk Free Rate: ", "Update RfR", _rfr.ToString("P2"), 0, 0);
            decimal rfr = 0;
            input.Replace("%", "");
            if (decimal.TryParse(input, out rfr))
            {
                _rfr = (rfr / 100);
                tradefiles_SelectedIndexChanged(null, null);
            }
        }

        void changecomm(object s, EventArgs e)
        {
            string input = Microsoft.VisualBasic.Interaction.InputBox("Per Contract Comission: ", "Update Comission", _comm.ToString("F6"), 0, 0);
            decimal c = 0;
            if (decimal.TryParse(input, out c))
            {
                _comm = c;
                tradefiles_SelectedIndexChanged(null, null);
            }
        }
        void TattleMain_MouseEnter(object sender, EventArgs e)
        {
            dg.AutoResizeColumnHeadersHeight();
        }

        void WatchPath() { WatchPath(Path); }
        void WatchPath(string path)
        {
            if (!_autowatch) return;
            fw = new FileSystemWatcher(path, "*" + RESULTS_POSTFIX);
            fw.IncludeSubdirectories = false;
            fw.EnableRaisingEvents = true;
            fw.Created += new FileSystemEventHandler(fw_Created);
            fw.Renamed += new RenamedEventHandler(fw_Renamed);
            fw.Deleted += new FileSystemEventHandler(fw_Deleted);
            fw.Changed += new FileSystemEventHandler(fw_Changed);
            ResetFiles(path);
        }

        /// <summary>
        /// clears results
        /// </summary>
        public void Clear()
        {
            if (InvokeRequired)
                Invoke(new VoidDelegate(Clear));
            else
            {
                dt.Clear();
                allresults.Items.Clear();
                _resultlists.Clear();
                refreshgrid();
            }
        }


        /// <summary>
        /// rediscover files in a path
        /// </summary>
        public void ResetFiles() { ResetFiles(Path); }
        public void ResetFiles(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] fis = di.GetFiles("*" + RESULTS_POSTFIX);
            foreach (FileInfo fi in fis)
            {
                NewResultFile(fi.Name);
            }
            // display last one
            DisplayResults(allresults.SelectedIndex);
        }

        void fw_Changed(object sender, FileSystemEventArgs e)
        {
            if (!_autowatch) return;
            DisplayResults(Results.FetchResults(LoadResults(e.Name),_rfr,_comm,debug));
            allresults.SelectedItem = e.Name;
        }

        void fw_Deleted(object sender, FileSystemEventArgs e)
        {
            if (!_autowatch) return;
            remresult(e.Name);
        }

        void fw_Renamed(object sender, RenamedEventArgs e)
        {
            if (!_autowatch) return;
            remresult(e.Name);
        }

        void fw_Created(object sender, FileSystemEventArgs e)
        {
            NewResultFile(e.Name);
        }

        List<Result> _resultlists = new List<Result>();

        /// <summary>
        /// remove a result from list of results 
        /// (if result was loaded from file, does not delete file).
        /// </summary>
        /// <param name="name"></param>
        public void remresult(string name)
        {
            if (allresults.InvokeRequired)
                Invoke(new DebugDelegate(remresult), new object[] { name });
            else if (name.Contains(RESULTS_POSTFIX))
            {
                int idx = NameIndex(name);
                _resultlists.RemoveAt(idx);
                allresults.Items.RemoveAt(idx);
                refreshgrid();
            }
        }

        /// <summary>
        /// determine if a result's name is unique (adding a result requires a unique name)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool isUniqueName(string name)
        {
            return NameIndex(name) == -1;
        }

        int NameIndex(string name)
        {
            for (int i = 0; i < allresults.Items.Count; i++)
                if (allresults.Items[i].ToString() == name) return i;
            return -1;
        }

        bool _autowatch = false;
        /// <summary>
        /// determines whether TradeResults will automatically add/remove result
        /// files that show up in 'Path'
        /// </summary>
        public bool AutoWatch { get { return _autowatch; } set { _autowatch = value; } }

        delegate void newresulttradesdel(string name, List<Trade> trades);

        /// <summary>
        /// add a result from list of trades
        /// </summary>
        /// <param name="name"></param>
        /// <param name="trades"></param>
        public void NewResultTrades(string name, List<Trade> trades)
        {
            if (allresults.InvokeRequired)
                Invoke(new newresulttradesdel(NewResultTrades), new object[] { name,trades });
            else
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
                if (!isUniqueName(name)) return;
                List<TradeResult> newresult;
                if (trades.Count == 0)
                {
                    debug("No results found for: " + name);
                    newresult = new List<TradeResult>();
                }
                else
                    newresult = TradeResult.ResultsFromTradeList(trades);
                allresults.Items.Add(name);
                int idx = allresults.Items.Count - 1;
                _resultlists.Add(Results.FetchResults(newresult, _rfr, _comm, debug));
                allresults.SelectedIndex = idx;
            }
        }

        public void NewResult(Result result)
        {
            if (InvokeRequired)
                Invoke(new ResultDel(NewResult), new object[] { result});
            else
            {
                allresults.Items.Add(result.ResultsId);
                int idx = allresults.Items.Count - 1;
                _resultlists.Add(result);
                allresults.SelectedIndex = idx;
            }
        }

        /// <summary>
        /// add a result from a file
        /// </summary>
        /// <param name="filename"></param>
        public void NewResultFile(string filename)
        {
            if (allresults.InvokeRequired)
                Invoke(new DebugDelegate(NewResultFile), new object[] { filename });
            else
            {
                if (!isUniqueName(filename)) return;
                List<TradeResult> results = LoadResults(filename);
                if (results.Count == 0)
                {
                    debug("No results found for "+filename);
                    return;
                }
                allresults.Items.Add(filename);
                int idx = allresults.Items.Count - 1;
                _resultlists.Add(Results.FetchResults(results,_rfr,_comm,debug));
                allresults.SelectedIndex = idx;
            }
        }

        void tradefiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (allresults.SelectedIndex < 0) return;
            DisplayResults(allresults.SelectedIndex);
        }


        void debug(string msg)
        {
            if (SendDebug != null)
                SendDebug(msg);
        }

        public event DebugDelegate SendDebug;

        

        List<TradeResult> LoadResults(string filename)
        {
            if ((filename == null) || !filename.Contains(RESULTS_POSTFIX))  
                return new List<TradeResult>();
            StreamReader sr;
            List<TradeResult> results = new List<TradeResult>(100);
            try
            {
                sr = new StreamReader(_watchpath + @"\" + filename);
                sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    results.Add(TradeResult.Init(sr.ReadLine()));
                }
                sr.Close();
            }
            catch (Exception) { }
            return results;

        }


        /// <summary>
        /// change selected index
        /// </summary>
        /// <param name="name"></param>
        public void DisplayResults(string name)
        {
            int idx = NameIndex(name) ;
            if (idx== -1) return;
            allresults.SelectedIndex = idx;
        }

        void DisplayResults(int idx)
        {
            if ((idx<0)  || (idx>=_resultlists.Count))
                return;
            DisplayResults(_resultlists[idx]);
        }

        public Result CurrentResults = new Results();

        void DisplayResults(Result r)
        {
            CurrentResults = r;
            dt.BeginLoadData();
            dt.Clear();
            Type t = r.GetType();
            FieldInfo[] fis = t.GetFields();
            foreach (FieldInfo fi in fis)
            {
                string format = null;
                if (fi.FieldType == typeof(Decimal)) format = "{0:N2}";
                dt.Rows.Add(fi.Name, (format != null) ? string.Format(format, fi.GetValue(r)) : fi.GetValue(r).ToString());
            }
            PropertyInfo[] pis = t.GetProperties();
            foreach (PropertyInfo pi in pis)
            {
                if (pi.Name == "PerSymbolStats")
                    continue;
                string format = null;
                if (pi.PropertyType == typeof(Decimal)) format = "{0:N2}";
                dt.Rows.Add(pi.Name, (format != null) ? string.Format(format, pi.GetValue(r, null)) : pi.GetValue(r, null).ToString());
            }
            foreach (string ps in r.PerSymbolStats)
            {
                string[] rs= ps.Split(':');
                if (rs.Length != 2) continue;
                dt.Rows.Add(rs[0], rs[1]);
            }
            dt.EndLoadData();
            refreshgrid();
        }

        void refreshgrid()
        {

            dg.Invalidate();

        }

        private void allresults_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }



    public class TradeResult : TradeLink.Common.TradeImpl
    {
        public override string ToString()
        {
            return Source.ToString()+" cpl: "+ClosedPL.ToString("F2");
        }
        public Trade Source;
        public decimal OpenPL;
        public decimal ClosedPL;
        public int OpenSize;
        public int ClosedSize;
        public decimal AvgPrice;
        const int s = 7;
        // we're reading these values from file, 
        // bc it's faster than recalculating each time
        public static TradeResult Init(string resultline)
        {
            string[] res = resultline.Split(',');
            TradeResult r = new TradeResult();
            r.Source = TradeLink.Common.TradeImpl.FromString(resultline);
            if (res[s] != string.Empty || res[s] != "")
                r.OpenPL = Convert.ToDecimal(res[s], System.Globalization.CultureInfo.InvariantCulture);
            if (res[s + 1] != string.Empty || res[s + 1] != "")
                r.ClosedPL = Convert.ToDecimal(res[s + 1], System.Globalization.CultureInfo.InvariantCulture);
            if (res[s + 2] != string.Empty || res[s + 2] != "")
                r.OpenSize = Convert.ToInt32(res[s + 2], System.Globalization.CultureInfo.InvariantCulture);
            if (res[s + 3] != string.Empty || res[s + 3] != "")
                r.ClosedSize = Convert.ToInt32(res[s + 3], System.Globalization.CultureInfo.InvariantCulture);
            if (res[s + 4] != string.Empty || res[s + 4] != "")
                r.AvgPrice = Convert.ToDecimal(res[s + 4], System.Globalization.CultureInfo.InvariantCulture);
            return r;
        }


        public static List<TradeResult> ResultsFromTradeList(List<Trade> trades)
        {
            string[] results = Util.TradesToClosedPL(trades);
            List<TradeResult> tresults = new List<TradeResult>(results.Length);
            foreach (string line in results)
                tresults.Add(TradeResult.Init(line));
            return tresults;
        }



    }

}
