using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using System.Net;
using TradeLink.API;
using Microsoft.Win32;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace TradeLink.Common
{
	public class ChimeraDataUtils
	{
		public static TickImpl GetTradeTick(string strLine)
		{
			var values = strLine.Split(',');
			if (values.Length < 11)
				return new TickImpl();
			string strSym = values[2];
			TickImpl q = new TickImpl(strSym);

			//Date
			string strDate = values[0];
			q.date = Convert.ToInt32(strDate);
			//Time
			string strTime = values[1];
			strTime = Regex.Replace(strTime, @":", "").Substring(0, 6);
			q.time = Convert.ToInt32(strTime);
			//DateTime
			string strDateTime = strDate + strTime;
			q.datetime = Convert.ToInt64(strDateTime);
			//TradePrice
			string strScale = values[6];
			string strTradePrice = values[9];

			int numDecPlace = Convert.ToInt32(Math.Log10(Convert.ToDouble(1 / Const.IPRECV)));
			int appendZeros = numDecPlace - Convert.ToInt32(strScale);

			strTradePrice = appendZeros > 0 ? strTradePrice + new string('0', appendZeros) : strTradePrice.Substring(0, strTradePrice.Length + appendZeros);
			q._trade = (ulong)Convert.ToInt64(strTradePrice);
			
			// TradeSize
			string strTradeSize = values[10];
			q.size = Convert.ToInt32(strTradeSize);

			// Exchange
			q.ex = values[8];

			return q;
		}
		public static TickImpl GetQuoteTick(string strLine)
		{
			var values = strLine.Split(',');
			if (values.Length < 14)
				return new TickImpl();
			string strSym = values[2];
			TickImpl q = new TickImpl(strSym);

			//Date
			string strDate = values[0];
			q.date = Convert.ToInt32(strDate);
			//Time
			string strTime = values[1];
			strTime = Regex.Replace(strTime, @":", "").Substring(0, 6);
			q.time = Convert.ToInt32(strTime);
			//DateTime
			string strDateTime = strDate + strTime;
			q.datetime = Convert.ToInt64(strDateTime);
			//Bid / Ask
			string strScale = values[6];
			string strBid = values[9];
			string strAsk = values[12];

			int numDecPlace = Convert.ToInt32(Math.Log10(Convert.ToDouble(1 / Const.IPRECV)));
			int appendZeros = numDecPlace - Convert.ToInt32(strScale);

			strBid = appendZeros > 0 ? strBid + new string('0', appendZeros) : strBid.Substring(0, strBid.Length + appendZeros);
			strAsk = appendZeros > 0 ? strAsk + new string('0', appendZeros) : strAsk.Substring(0, strAsk.Length + appendZeros);
			q._bid = (ulong)Convert.ToInt64(strBid);
			q._ask = (ulong)Convert.ToInt64(strAsk);

			// BidSize / AskSize
			string strBidSize = values[10];
			string strAskSize = values[13];
			q.bs = Convert.ToInt32(strBidSize);
			q.os = Convert.ToInt32(strAskSize);

			// Exchange
			q.be = values[8];
			q.oe = values[11];

			return q;
		}
		public static SecurityImpl SecurityFromFileName(string strFile)
		{
			try
			{
				string strDate = strFile.Substring(15, 8);
				int symLength = strFile.Length - 41;
				string strSym = strFile.Substring(30, symLength);
				//string sym = strFile.Replace(ds, "").Replace(TikConst.DOT_EXT, "");
				SecurityImpl s = new SecurityImpl(strSym);
				s.Date = Convert.ToInt32(strDate);
				return s;
			}
			catch (System.Exception ex)
			{
				
			}
			return new SecurityImpl();
		}
		public static string QuotePathFromTradePath(string strPath)
		{
			string strQuote = strPath.Substring(0,30);
			int symLength = strPath.Length - 41;
			string strSym = strPath.Substring(30, symLength);
			strQuote = strQuote + strSym + "_quotes.csv";
			return strQuote;
		}
		public static string NBBOPathFromTradePath(string strPath)
		{
			string strNBBO = strPath.Substring(0, 30);
			int symLength = strPath.Length - 41;
			string strSym = strPath.Substring(30, symLength);
			strNBBO = strNBBO + strSym + "_nbbo.csv";
			return strNBBO;
		}
		public static TickFileInfo ParseFile(string filepath)
		{
			TickFileInfo tfi;
			tfi.type = TickFileType.Invalid;
			tfi.date = DateTime.MinValue;
			tfi.symbol = "";

			try
			{
				string fn = System.IO.Path.GetFileNameWithoutExtension(filepath);
				string ext = System.IO.Path.GetExtension(filepath).Replace(".", "");

				SecurityImpl s = SecurityFromFileName(filepath);

				tfi.type = (TickFileType)Enum.Parse(typeof(TickFileType), ext.ToUpper());
				tfi.date = Util.TLD2DT(s.Date);
				tfi.symbol = s.symbol;
			}
			catch (Exception) { tfi.type = TickFileType.Invalid; }
			return tfi;
		}

		/// <summary>
		/// builds list of readable tick files with given extension found in top level of folder
		/// </summary>
		/// <param name="Folder"></param>
		/// <param name="tickext"></param>
		/// <returns></returns>
		public static string[,] TickFileIndex(string Folder, string tickext) { return TickFileIndex(Folder, tickext, false, null); }/// <summary>
		/// builds list of readable tick files found in given folder
		/// </summary>
		/// <param name="Folder">path containing tickfiles</param>
		/// <param name="tickext">file extension</param>
		/// <returns></returns>
		public static string[,] TickFileIndex(string Folder, string tickext, bool searchSubFolders) { return TickFileIndex(Folder, tickext, searchSubFolders, null); }
		/// <summary>
		/// builds list of readable tick files (and their byte-size) found in folder
		/// </summary>
		/// <param name="Folder"></param>
		/// <param name="tickext"></param>
		/// <param name="searchSubFolders"></param>
		/// <param name="debug"></param>
		/// <returns></returns>
		public static string[,] TickFileIndex(string Folder, string tickext, bool searchSubFolders, DebugDelegate debug)
		{
			//string[] _tickfiles = Directory.GetFiles(Folder, tickext);
			DirectoryInfo di = new DirectoryInfo(Folder);
			FileInfo[] fi = di.GetFiles(tickext, searchSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			string[,] index = new string[fi.Length, 2];
			int i = 0;
			int qtr = fi.Length / 4;
			foreach (FileInfo thisfi in fi)
			{
				if ((debug != null) && (i % qtr == 0))
					debug((fi.Length - i).ToString("N0") + " files remaining to index...");
				index[i, 0] = searchSubFolders ? thisfi.FullName : thisfi.Name;
				index[i, 1] = thisfi.Length.ToString();
				i++;
			}
			return index;
		}
        
        

	}
}
