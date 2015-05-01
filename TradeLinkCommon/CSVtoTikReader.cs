using System;
using System.Collections.Generic;
using System.Text;
using TradeLink.API;
using System.IO;
using System.Text.RegularExpressions;

namespace TradeLink.Common
{
	/// <summary>
	/// read tradelink tick files
	/// </summary>
	public class CSVtoTikReader : StreamReader
	{
		private TickImpl nextQuote = new TickImpl();
		private TickImpl nextTrade = new TickImpl();
		private TickImpl nextNBBO = new TickImpl();

		private StreamReader nbboReader;
		private StreamReader quotesReader;
		string _realsymbol = string.Empty;
		string _sym = string.Empty;
		Security _sec = new TradeLink.Common.SecurityImpl();
		string _path = string.Empty;
		/// <summary>
		/// estimate of ticks contained in file
		/// </summary>
		public int ApproxTicks = 0;
		/// <summary>
		/// real symbol for data represented in file
		/// </summary>
		public string RealSymbol { get { return _realsymbol; } }
		/// <summary>
		/// security-parsed symbol
		/// </summary>
		public string Symbol { get { return _sym; } }
		/// <summary>
		/// security represented by parsing realsymbol
		/// </summary>
		/// <returns></returns>
		public Security ToSecurity() { return _sec; }
		/// <summary>
		/// file is readable, has version and real symbol
		/// </summary>
		public bool isValid { get { return (_filever != 0) && (_realsymbol != string.Empty) && BaseStream.CanRead; } }
		/// <summary>
		/// count of ticks presently read
		/// </summary>
		public int Count = 0;
		public CSVtoTikReader(string strTradesPath, string strQuotesPath, string strNbboPath, string sym, int date)
			: base(new FileStream(strTradesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
		{
			quotesReader = new StreamReader(new FileStream(strQuotesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			nbboReader = new StreamReader(new FileStream(strNbboPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			_path = strTradesPath;
			FileInfo fi = new FileInfo(strTradesPath);
			ApproxTicks = (int)((double)fi.Length / 39);
			_realsymbol = sym;
			_sec = TradeLink.Common.SecurityImpl.Parse(_realsymbol);
			_sec.Date = ChimeraDataUtils.SecurityFromFileName(_path).Date;
			// get short symbol
			_sym = _sec.symbol;

			ReadHeader();
		}
		bool _endOfQuoteStream = false;
		bool _endOfTradeStream = false;
		bool _haveQuote = false;
		bool _haveTrade = false;
		bool _haveheader = false;
		int _filever = 0;

		bool ReadNewQuote()
		{
			try
			{
				nextQuote = ChimeraDataUtils.GetQuoteTick(quotesReader.ReadLine());
			}
			catch (EndOfStreamException)
			{
				_endOfQuoteStream = true;
				_haveQuote = false;
				quotesReader.Close();
				return false;
			}
			catch (ObjectDisposedException)
			{
				_endOfQuoteStream = true;
				_haveQuote = false;
				return false;
			}
			catch (System.Exception ex)
			{
				_endOfQuoteStream = true;
				_haveQuote = false;
				return false;
			}
			_haveQuote = true;
			return true;
		}
		bool ReadNewTrade()
		{
			try
			{
				nextTrade = ChimeraDataUtils.GetTradeTick(this.ReadLine());
			}
			catch (EndOfStreamException)
			{
				_endOfTradeStream = true;
				_haveTrade = false;
				this.Close();
				return false;
			}
			catch (ObjectDisposedException)
			{
				_endOfTradeStream = true;
				_haveTrade = false;
				return false;
			}
			catch (System.Exception ex)
			{
				_endOfTradeStream = true;
				_haveTrade = false;
				return false;
			}
			_haveTrade = true;
			return true;
		}
		void ReadHeader()
		{
			// get version id
			//ReadByte();
			// get version
			_filever = 1; //ReadInt32();
			
			/*// get real symbol
			_realsymbol = ReadString();*/
			// get security from symbol
			// get end of header
			//ReadByte();
			// make sure we read something
			if (_realsymbol.Length <= 0)
				throw new BadTikFile("no symbol defined in tickfile");
			// flag header as read
			_haveheader = true;


			// Prime first ticks in each file
			ReadNewQuote();
			ReadNewTrade();

			// flag header as read
			_haveheader = true;
		}

		public event TickDelegate gotTick;

		/// <summary>
		/// returns true if more data to process, false otherwise
		/// </summary>
		/// <returns></returns>
		public bool NextTick()
		{
			if (!_haveheader)
				ReadHeader();

			try
			{
				// prepare a tick
				TickImpl k;// = new TradeLink.Common.TickImpl(_realsymbol);

				if (_haveTrade && nextTrade.time <= nextQuote.time)
				{
					k = nextTrade;
					if (!_endOfTradeStream)
					{
						ReadNewTrade();
					}
				}
				else if (_haveQuote)
				{
					k = nextQuote;

					if (!_endOfQuoteStream)
					{
						ReadNewQuote();
					}
				}
				else 
					return false;
				

				// send any tick we have
				if (gotTick != null)
					gotTick(k);
				// count it
				Count++;
				// assume there is more
				return true;
			}
			catch (EndOfStreamException)
			{
				Close();
				return false;
			}
			catch (ObjectDisposedException)
			{
				return false;
			}
		}
	}

	public class BadTikFile : Exception
	{
		public BadTikFile() : base() { }
		public BadTikFile(string message) : base(message) { }
	}
}
