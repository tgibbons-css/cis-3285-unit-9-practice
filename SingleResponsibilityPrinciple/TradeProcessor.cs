using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SingleResponsibilityPrinciple
{
    public class TradeProcessor
    {
        const float LotSize = 100000f;

        /// <summary>
        /// Read the text file containing the trades. This file should in in the format of one trade per line
        ///    GBPUSD,1000,1.51
        /// </summary>
        /// <param name="stream"> File must be passed in as a Stream. </param>
        /// <returns> Returns a list of strings, one for each string for each line in the file </returns>
        public IEnumerable<string> ReadTradeData(Stream stream)
        {
            // read rows
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
            }
            return lines;
        }

        /// <summary>
        /// Checks the formate on a single line in the trade file.
        /// </summary>
        /// <param name="fields"> The string must be split into three components before calling </param>
        /// <param name="currentLine"> This is the current line number in the file, used to report errors</param>
        /// <returns> true if all the checks pass </returns>
        private bool ValidateTradeData(String[] fields, int currentLine)
        {
            if (fields.Length != 3)
            {
                LogMessage("WARN: Line {0} malformed. Only {1} field(s) found.", currentLine, fields.Length);
                return false;
            }

            if (fields[0].Length != 6)
            {
                LogMessage("WARN: Trade currencies on line {0} malformed: '{1}'", currentLine, fields[0]);
                return false;
            }

            int tradeAmount;
            if (!int.TryParse(fields[1], out tradeAmount))
            {
                LogMessage("WARN: Trade amount on line {0} not a valid integer: '{1}'", currentLine, fields[1]);
                return false;
            }

            decimal tradePrice;
            if (!decimal.TryParse(fields[2], out tradePrice))
            {
                LogMessage("WARN: Trade price on line {0} not a valid decimal: '{1}'", currentLine, fields[2]);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Converts a string containing the trade data into a TradeRecord object
        /// </summary>
        /// <param name="fields"> The string must be split into three components before calling </param>
        /// <returns> A TradeRecord object containing the trade data</returns>
        private TradeRecord MapTradeDataToTradeRecord(String[] fields)
        {
            string sourceCurrencyCode = fields[0].Substring(0, 3);
            string destinationCurrencyCode = fields[0].Substring(3, 3);
            int tradeAmount = int.Parse(fields[1]);
            decimal tradePrice = decimal.Parse(fields[2]);

            // calculate values
            TradeRecord trade = new TradeRecord();
            trade.SourceCurrency = sourceCurrencyCode;
            trade.DestinationCurrency = destinationCurrencyCode;
            trade.Lots = tradeAmount / LotSize;
            trade.Price = tradePrice;

            return trade;
        }

        /// <summary>
        /// Takes a list of strings containing trade data and converts this into a list of TradeRecord objects
        /// </summary>
        /// <param name="lines"> The strings containing the trade data, each string should contain one trade in format of "GBPUSD,1000,1.51"</param>
        /// <returns> A list of TradeRecords, one record for each trade </returns>
        private IEnumerable<TradeRecord> ParseTrades(IEnumerable<string> lines)
        {
            List<TradeRecord> trades = new List<TradeRecord>();

            int lineCount = 1;
            foreach (var line in lines)
            {
                String[] fields = line.Split(new char[] { ',' });

                if (ValidateTradeData(fields, lineCount) == false)
                {
                    continue;
                }

                TradeRecord trade = MapTradeDataToTradeRecord(fields);
                trades.Add(trade);

                lineCount++;
            }
            return trades;
        }
        /// <summary>
        /// Write the trade records to the database
        /// </summary>
        /// <param name="trades"> A list of TradeRecord objects </param>
        private void StoreTrades(IEnumerable<TradeRecord> trades)
        {
            LogMessage("INFO: Connecting to database");
            // The first connection string uses |DataDirectory| 
            //    and assumes the tradedatabase.mdf file is stored in 
            //    SingleResponsibilityPrinciple\bin\Debug 
            using (var connection = new System.Data.SqlClient.SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\tradedatabase.mdf;Integrated Security=True;Connect Timeout=30;"))
            // Template for connection string from database connection file
            //    The @ sign allows for back slashes
            //    Watch for double quotes which must be escaped using "" 
            //    Watch for extra spaces after C: and avoid paths with - hyphens -
            //    using (var connection = new System.Data.SqlClient.SqlConnection(@"  ;"))
            //using (SqlConnection connection = new System.Data.SqlClient.SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\tgibbons\source\repos\cis-3285-unit-8-f2020-tgibbons-css\Database\tradedatabase.mdf;Integrated Security=True;Connect Timeout=30"))
            
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    foreach (TradeRecord trade in trades)
                    {
                        SqlCommand command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.CommandText = "dbo.insert_trade";
                        command.Parameters.AddWithValue("@sourceCurrency", trade.SourceCurrency);
                        command.Parameters.AddWithValue("@destinationCurrency", trade.DestinationCurrency);
                        command.Parameters.AddWithValue("@lots", trade.Lots);
                        command.Parameters.AddWithValue("@price", trade.Price);

                        command.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                connection.Close();
            }

            LogMessage("INFO: {0} trades processed", trades.Count());
        }

        private void LogMessage(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        /// <summary>
        /// Main routine that processes the trade file
        /// </summary>
        /// <param name="stream"> The text file contianing the trade data </param>
        public void ProcessTrades(Stream stream)
        {
            var lines = ReadTradeData(stream);
            var trades = ParseTrades(lines);
            StoreTrades(trades);
        }

    }
}
