using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common.Objects;
using Newtonsoft.Json.Linq;

namespace Common
{
    public static class Commands
    {
        public static async Task GetTotalHourly(string WhoCalledMe, FunctionClass fc)
        {
            // take the hour here so we get the time when we ran the script
            var thisHour = DateTime.Now.ToShortDateString() + "," + DateTime.Now.ToShortTimeString();
            var now = DateTime.Now;

            var allClients = await ClientData.GetClientsDict();

            gSheets.Init(Paths.MomentGoogleCredentials);
            var aumClients = gSheets.readValues(Config.HourlyAumSpreadsheetId, $"Clients List!A2:ZZ");

            var allClientsAum = new List<string[]>();
            foreach (var aumClient in aumClients)
            {
                var clientName = aumClient[0];
                var exists = allClients.TryGetValue(clientName, out var client);
                if (!exists)
                {
                    Logger.Error($"client: '{clientName}' does not exist in our client list");
                    return;
                }

                var clientAum = new ClientAum
                {
                    DateTime = now,
                    ClientName = client.Name
                };

                for (int i = 0; i < 5; i++) // retry 5 times
                {
                    try
                    {
                        if (client.Platform == Platform.Binance)
                        {
                            clientAum.AumValue = await binanceCommands.getAccountTotal(client.ApiKey, client.ApiSecret, client.Name, false);
                        }
                        else
                        {
                            clientAum.AumValue = (await OkexCommands.getClientTotal(fc, client.ApiKey, client.ApiSecret, client.Passphrase, client.Name,
                                WhoCalledMe,
                                client.BaseCurrency)).Split(',')[0];
                        }

                        break;
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("Too Many Requests"))
                        {
                            Logger.Warn($"Failed to fetch total aum with exception, attempt number: {i + 1}", e);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                var ex = clientAum.Validate();
                if (ex != null)
                {
                    throw ex;
                }

                allClientsAum.Add(clientAum.ToStringsArray());

                fc.WriteMessageToFile(WhoCalledMe, "totalAum.csv", string.Join(",", clientAum));
            }

            try
            {
                gSheets.insertValuesAbove(Config.HourlyAumSpreadsheetId, $"", $"Hourly Aum!A2:G", $"Hourly Aum!A2:G",
                    allClientsAum.ToArray(), 2500);
            }
            catch (Exception e)
            {
                Logger.Error("ExecuteCommand error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", e.ToString());
            }
        }

        public static async Task GetHourlyPrices()
        {
            Logger.Info("Fetching prices for GetHourlyPrices");

            var hourlyPrice = new HourlyPrice();

            hourlyPrice.AddPrice(await binanceCommands.getSpotPrice("BTC-USDT"));
            hourlyPrice.AddPrice(await binanceCommands.getSpotPrice("ETH-USDT"));
            hourlyPrice.AddPrice(await binanceCommands.getSpotPrice("XLM-USDT"));

            var validation = hourlyPrice.Validate();
            if (validation != null)
            {
                throw validation;
            }

            var finalWriteValues = new string[1][];
            finalWriteValues[0] = hourlyPrice.ToStringsArray();

            gSheets.Init(Paths.MomentGoogleCredentials);
            gSheets.insertValuesAbove(Config.HourlyAumSpreadsheetId, $"", "Hourly Prices!A2:E",
                "Hourly Prices!A2:E", finalWriteValues, 200);
        }

        public static async Task Transfer(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client Client)
        {
            var transferFrom = m_command.GetParam(0);
            var fromId = m_command.GetParam(1);
            var transferCurrency = m_command.GetParam(2);
            var transferAmount = m_command.GetParam(3); // we take the paramaters from the command
            var transferTo = m_command.GetParam(4);
            var toId = m_command.GetParam(5);
            if (transferFrom == "N/A") // we dont transfer if the transfer is na
            {
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "transfer is NA,");
                return;
            }

            double secondssincelastcall = Client.TimeSinceLastCalled_All_Available_Funds();

            double howMuchToWait = 1000;

            if (secondssincelastcall < 20)
            {
                howMuchToWait = 20000 - secondssincelastcall * 1000;
                howMuchToWait = Convert.ToInt32(howMuchToWait);
            }

            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Sorry Mister ; going to wait " + howMuchToWait / 1000 + " sec");
            if (platform == "OKEX")
            {
                Thread.Sleep(Convert.ToInt32(howMuchToWait));
            }

            if (transferAmount == "All_Available_Funds")
            {
                try
                {
                    // if its all avilable funds we take the avialble funds from okex    
                    if (platform == "OKEX")
                    {
                        transferAmount = await OkexCommands.Get_max(transferFrom, transferCurrency, fromId, Client.ApiKey,
                            Client.ApiSecret, Client.Passphrase, fc,
                            WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        transferAmount = await binanceCommands.getMax(transferFrom, transferCurrency, fromId, Client.ApiKey, Client.ApiSecret);
                    }

                    // try again
                    if (transferAmount == "")
                    {
                        Thread.Sleep(Convert.ToInt32(10000));
                        if (platform == "OKEX")
                        {
                            transferAmount = await OkexCommands.Get_max(transferFrom, transferCurrency, fromId,
                                Client.ApiKey, Client.ApiSecret, Client.Passphrase, fc, WhoCalledMe);
                        }

                        if (platform == "BINANCE")
                        {
                            transferAmount = await binanceCommands.getMax(transferFrom, transferCurrency, fromId, Client.ApiKey, Client.ApiSecret);
                        }
                    }

                    if (transferAmount != "nothing in future instrument")
                    {
                        if (Convert.ToDouble(transferAmount) < 0.0001)
                        {
                            transferAmount = "nothing in future instrument";
                        }
                    }

                    if (transferAmount == "nothing in future instrument")
                    {
                        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "no money to transfer,");
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("ExecuteCommand error with exception", e);
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "ExecuteCommand Transfer All_Available_Funds," + e.ToString());
                }
            }

            try
            {
                // Transfer only amounts that are greater than 0
                if (Convert.ToDouble(transferAmount) > 0)
                {
                    // we transfer the funds
                    if (platform == "OKEX")
                    {
                        transferAmount = DecimalsHelper.ToString(float.Parse(transferAmount) * 0.996); // trying to fix transfer amoutn thing problem
                        await TransferMoney(Client, fc, transferFrom, transferCurrency, transferAmount, transferTo, fromId, toId, WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        var Instrument = toId;
                        if (toId == "N/A")
                        {
                            Instrument = fromId;
                        }

                        int x = 0;
                        while (x < 5)
                        {
                            try
                            {
                                x++;
                                await binanceCommands.transfer(transferCurrency, transferAmount,
                                    $"{transferFrom.Substring(0, 1).ToLower()}{transferFrom.Remove(0, 1)}{transferTo.Substring(0, 1).ToUpper()}{transferTo.Remove(0, 1)}",
                                    Client.ApiKey, Client.ApiSecret, Instrument);
                                break;
                            }
                            catch (BinanceErrorResponseException e)
                            {
                                transferAmount = DecimalsHelper.ToString(float.Parse(transferAmount) * 0.99);

                                if (!e.Message.Contains("Exceeding the maximum"))
                                {
                                    Logger.Error("Transfer error with exception", e);
                                    break;
                                }

                                Logger.Error("Transfer error with too much amount, retrying with lower amount", e);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Transfer error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "ExecuteCommand Transfer TransferMoney," + e);
            }
        }

        public static void RedLightsReport(string WhoCalledMe, FunctionClass fc)
        {
            gSheets.OldInit(Paths.CredentialsAmitJson);
            string[][] okexRaw = gSheets.readValues(Config.MainSpreadsheetId, "BTC Red Lights!A1:ZZ");
            string[][] binanceRaw = gSheets.readValues(Config.BinanceSpreadsheetId, "BTC Red Lights!A1:ZZ");
            List<string> okexProblematicLines = new List<string>();
            List<string> binanceProblematicLines = new List<string>();

            List<string> posSizeOkex = new List<string>();
            List<string> liqPriceOkex = new List<string>();
            List<string> posSizeBinance = new List<string>();
            List<string> liqPriceBinance = new List<string>();
            for (int i = 0; i < okexRaw.Length; i++)
            {
                try
                {
                    if (okexRaw[i][26] == "0") // we have a problem here so we look at it and add it to list of problems
                    {
                        okexProblematicLines.Add($"{okexRaw[i][2]},{okexRaw[i][3]},{okexRaw[i][4]}");
                        posSizeOkex.Add($"{okexRaw[i][2]},{okexRaw[i][3]},{okexRaw[i][4]},{okexRaw[i][24]},{okexRaw[i][25]},");
                    }

                    if (okexRaw[i][30] == "0")
                    {
                        okexProblematicLines.Add($"{okexRaw[i][2]},{okexRaw[i][3]},{okexRaw[i][4]}");
                        liqPriceOkex.Add($"{okexRaw[i][2]},{okexRaw[i][3]},{okexRaw[i][4]},{okexRaw[i][28]},{okexRaw[i][29]},");
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("ExecuteCommand error with exception", e);
                }
            }

            for (int i = 0; i < okexProblematicLines.Count - 1; i++)
            {
                if (okexProblematicLines[i] == okexProblematicLines[i + 1])
                {
                    okexProblematicLines.Remove(okexProblematicLines[i]); // removes duplicates
                }
            }

            string thisLinePos = "";
            string thisLineLiq = "";
            for (int i = 0; i < okexProblematicLines.Count; i++)
            {
                thisLinePos = ",";
                thisLineLiq = ",";
                for (int z = 0; z < posSizeOkex.Count; z++)
                {
                    if (okexProblematicLines[i].Split(',')[0] + okexProblematicLines[i].Split(',')[1] + okexProblematicLines[i].Split(',')[2] ==
                        posSizeOkex[z].Split(',')[0] + posSizeOkex[z].Split(',')[1] + posSizeOkex[z].Split(',')[2])
                    {
                        thisLinePos = posSizeOkex[z].Split(',')[3] + "," + posSizeOkex[z].Split(',')[4] + ",";
                    }
                }

                okexProblematicLines[i] += thisLinePos;
                for (int z = 0; z < liqPriceOkex.Count; z++)
                {
                    if (okexProblematicLines[i].Split(',')[0] + okexProblematicLines[i].Split(',')[1] + okexProblematicLines[i].Split(',')[2] ==
                        liqPriceOkex[z].Split(',')[0] + liqPriceOkex[z].Split(',')[1] + liqPriceOkex[z].Split(',')[2])
                    {
                        thisLinePos = liqPriceOkex[z].Split(',')[3] + "," + liqPriceOkex[z].Split(',')[4];
                    }
                }

                okexProblematicLines[i] += thisLineLiq;
            }

            string okexFinalString = "client,instrument,exchange,expected pos,actual pos,expected liq,actual liq" + Environment.NewLine;
            for (int i = 0; i < okexProblematicLines.Count; i++)
            {
                okexFinalString += okexProblematicLines[i] + Environment.NewLine;
            }


            for (int i = 0; i < binanceRaw.Length; i++)
            {
                try
                {
                    if (binanceRaw[i][26] == "0") // we have a problem here so we look at it and add it to list of problems
                    {
                        binanceProblematicLines.Add($"{binanceRaw[i][2]},{binanceRaw[i][3]},{binanceRaw[i][4]}");
                        posSizeBinance.Add($"{binanceRaw[i][2]},{binanceRaw[i][3]},{binanceRaw[i][4]},{binanceRaw[i][24]},{binanceRaw[i][25]},");
                    }

                    if (binanceRaw[i][30] == "0")
                    {
                        binanceProblematicLines.Add($"{binanceRaw[i][2]},{binanceRaw[i][3]},{binanceRaw[i][4]}");
                        liqPriceBinance.Add($"{binanceRaw[i][2]},{binanceRaw[i][3]},{binanceRaw[i][4]},{binanceRaw[i][28]},{binanceRaw[i][29]},");
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("ExecuteCommand error with exception", e);
                }
            }

            for (int i = 0; i < binanceProblematicLines.Count - 1; i++)
            {
                if (binanceProblematicLines[i] == binanceProblematicLines[i + 1])
                {
                    binanceProblematicLines.Remove(binanceProblematicLines[i]); // removes duplicates
                }
            }

            thisLinePos = "";
            thisLineLiq = "";
            for (int i = 0; i < binanceProblematicLines.Count; i++)
            {
                thisLinePos = ",";
                thisLineLiq = ",";
                for (int z = 0; z < posSizeBinance.Count; z++)
                {
                    if (binanceProblematicLines[i].Split(',')[0] + binanceProblematicLines[i].Split(',')[1] + binanceProblematicLines[i].Split(',')[2] ==
                        posSizeBinance[z].Split(',')[0] + posSizeBinance[z].Split(',')[1] + posSizeBinance[z].Split(',')[2])
                    {
                        thisLinePos = posSizeBinance[z].Split(',')[3] + "," + posSizeBinance[z].Split(',')[4] + ",";
                    }
                }

                binanceProblematicLines[i] += thisLinePos;
                for (int z = 0; z < liqPriceBinance.Count; z++)
                {
                    if (binanceProblematicLines[i].Split(',')[0] + binanceProblematicLines[i].Split(',')[1] + binanceProblematicLines[i].Split(',')[2] ==
                        liqPriceBinance[z].Split(',')[0] + liqPriceBinance[z].Split(',')[1] + liqPriceBinance[z].Split(',')[2])
                    {
                        thisLinePos = liqPriceBinance[z].Split(',')[3] + "," + liqPriceBinance[z].Split(',')[4];
                    }
                }

                binanceProblematicLines[i] += thisLineLiq;
            }

            string binanceFinalString = "client,instrument,exchange,expected pos,actual pos,expected liq,actual liq" + Environment.NewLine;
            for (int i = 0; i < binanceProblematicLines.Count; i++)
            {
                binanceFinalString += binanceProblematicLines[i] + Environment.NewLine;
            }

            fc.WriteMessageToFile(WhoCalledMe, "redLights.csv", okexFinalString);
            fc.WriteMessageToFile(WhoCalledMe, "redLights.csv", binanceFinalString);
        }

        public static async Task TradeExecutionNot(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var Exchange = m_command.GetParam(0);
            var Instrument = m_command.GetParam(1);
            var levOrNo = m_command.GetParam(2);
            var Leverage = m_command.GetParam(3);
            var Type = m_command.GetParam(4);
            var Ordertype = m_command.GetParam(5);
            var Size = m_command.GetParam(6);
            var PriceVariance = m_command.GetParam(7);
            var SweepRatio = m_command.GetParam(8);
            var Sizelimit = m_command.GetParam(9);
            var PriceLimit = m_command.GetParam(10);
            var TimeInterval = m_command.GetParam(11);

            if (Exchange == "N/A" || Sizelimit == "0" || Size == "-0")
            {
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Trade execution is NA");
                return;
            }

            string Direction = "";
            bool canEnter = false;
            try
            {
                if (double.Parse(Size) > 0)
                {
                    canEnter = true;
                }
            }
            catch (Exception e)
            {
                Logger.Error("ExecuteCommand error with exception", e);
                if (Size == "All")
                {
                    canEnter = true;
                }
            }

            if (canEnter) // convert size to double and make sure its above 0
            {
                if (Type == "1" || Type == "4") // 1 = open long, 4 = close short
                {
                    Direction = "long";
                }
                else if (Type == "2" || Type == "3") // 2 = open short, 3 = close long
                {
                    Direction = "short";
                }
                else
                {
                    throw new InvalidOperationException("Type is not defined");
                }

                try
                {
                    if (levOrNo == "Yes")
                    {
                        if (Exchange == "Futures" && platform == "BINANCE" || Exchange == "Swap" && platform == "BINANCE")
                        {
                            if (Exchange == "Swap")
                            {
                                await binanceCommands.changeSwapLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }

                            if (Exchange == "Futures")
                            {
                                await binanceCommands.changeFuturesLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }
                        }

                        if (Exchange == "OKEX")
                        {
                            if (Exchange == "Futures" && Ordertype == "4" || Exchange == "Swap")
                            {
                                await OkexCommands.Set_leverageFutures(fc, Exchange, Instrument, Direction, Leverage, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }
                            else if (Exchange == "Margin" && Ordertype == "4")
                            {
                                await OkexCommands.Set_leverageMargin(fc, Instrument, Leverage, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }
                        }
                    }

                    // if exchange is spot the programm ignores the leverage
                    await TradeExecution(WhoCalledMe, fc, platform, client, Exchange, Instrument, Type, Ordertype, Size, PriceVariance,
                        SweepRatio, Sizelimit, PriceLimit, TimeInterval);
                }
                catch (Exception e)
                {
                    Logger.Error("ExecuteCommand error with exception", e);
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Trade_execution ," + e.ToString());
                }
            }
            else
            {
                // don't trade on size 0;
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Trade execution size is 0");
            }
        }

        public static async Task SellAvailable(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var Exchange = m_command.GetParam(0);
            var instrument = m_command.GetParam(1);
            var qt = "";
            var coi = instrument.Split('-')[0];

            if (instrument != "N/A" && Exchange != "N/A")
            {
                // we call the command only if the params are not empty
                qt = await OkexCommands.get_borrowed(fc, instrument, client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe);
                qt = qt.Split(',')[3];
                await TradeExecution(WhoCalledMe, fc, platform, client, "Margin", instrument, "2", "4", qt, "0.005", "0.02", qt, "0", "5");
            }
            else
            {
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "sell all available is NA");
            }
        }

        public static async Task UploadPrices(string WhoCalledMe, FunctionClass fc)
        {
            //var getPricesLinesNumber = 12;
            await new SlackConnector(Config.FileSpaceChannelId, Config.SlackToken).SendFile(Path.Join(Paths.DataDir, "MainForm__outputGetPrices.csv"));
            //gSheets.Init(Paths.CredentialsAmitJson);
            //string[] pricesRaw = fc.ReadLines(WhoCalledMe + ",", "outputGetPrices.csv");
            //string[] prices = new string[getPricesLinesNumber];
            //if (prices.Length > getPricesLinesNumber)
            //{
            //    prices = pricesRaw.Skip(prices.Length - getPricesLinesNumber).ToArray();
            //}
            //else
            //{
            //    prices = pricesRaw;
            //}

            //string[][] pricesToUpload = new string[getPricesLinesNumber][];
            //string pricesStr = "";
            //for (int i = 0; i < getPricesLinesNumber; i++)
            //{
            //    pricesToUpload[i] = prices[i].Split(',').Skip(2).ToArray();
            //    pricesStr += prices[i] + Environment.NewLine;
            //}

            //gSheets.writeValues(Config.MainSpreadsheetId, "Prices and Actions !B7:G18", pricesToUpload);
            //pricesRaw = fc.ReadLines(WhoCalledMe, "outputGetPrices_binance.csv");
            //pricesToUpload = new string[pricesRaw.Length][];
            //fc.WriteMessageToFile("", $"automationData{DateTime.Now.Date.ToShortDateString().Replace('/', '_')}.csv",
            //    "okex prices " + Environment.NewLine + pricesStr + Environment.NewLine);
            //pricesStr = "";
            //for (int i = 0; i < pricesRaw.Length; i++)
            //{
            //    pricesToUpload[i] = pricesRaw[i].Split(',').Skip(2).ToArray();
            //    pricesStr += pricesRaw[i] + Environment.NewLine;
            //}

            //fc.WriteMessageToFile("", $"automationData{DateTime.Now.Date.ToShortDateString().Replace('/', '_')}.csv",
            //    "binance prices " + Environment.NewLine + pricesStr + Environment.NewLine);
            //gSheets.writeValues(Config.BinanceSpreadsheetId, "Prices and Actions !B7:E17", pricesToUpload);
        }

        public static async Task dailySymbol()
        {
            try
            {
                gSheets.Init(Paths.MomentGoogleCredentials);
                string[][] commands = gSheets.readValues(Config.DecisionsSpreadsheetId, "DailySymbol Input/Output!A6:B");
                List<dailySymbolRequest> symbols = new List<dailySymbolRequest>();
                for (int i = 0; i < commands.Length; i++)
                {
                    if (commands[i][0].ToLower() == "binance")
                    {
                        symbols.Add(new dailySymbolRequest(commands[i][1], "BINANCE"));
                    }
                    else if (commands[i][0].ToLower() == "okex")
                    {
                        symbols.Add(new dailySymbolRequest(commands[i][1], "OKEX"));
                    }
                    else
                    {
                        Logger.Error("dailySymbol platform is bad, exiting");
                        return;
                    }
                }

                Candle[] candles = await dailyS.dailySymbol(symbols);
                string[][] prints = new string[1][];
                prints[0] = new string[candles.Length];
                string all = ""; //sagi dont be mad pls

                for (int i = 0; i < candles.Length; i++)
                {
                    all += candles[i].toString();
                }

                prints[0] = all.Split(',');
                gSheets.writeValues(Config.DecisionsSpreadsheetId, "DailySymbol Input/Output!A2:ZZZ2", prints);
            }
            catch (Exception e)
            {
                Logger.Error("daily symbol failed " + e);
            }
        }

        public static async Task GetPrices(string WhoCalledMe, FunctionClass fc)
        {
            try
            {
                gSheets.Init(Paths.MomentGoogleCredentials);
                string[][] commands = gSheets.readValues(Config.AggCommandsSpreadsheetId, "Get Prices Input!A2:ZZ");
                string[][] prints = new string[commands.Length][];
                for (int i = 0; i < commands.Length; i++)
                {
                    prints[i] = await GetPrice(WhoCalledMe, fc, commands[i]);
                }

                gSheets.writeValues(Config.AggCommandsSpreadsheetId, "Get Prices Output!A2:ZZ", prints);
                await UploadPrices(WhoCalledMe, fc);
            }
            catch (Exception e)
            {
                Logger.Error("error with get prices " + e);
            }
        }

        public static async Task<string[]> GetPrice(string WhoCalledMe, FunctionClass fc, string[] command)
        {
            string platform = command[0];
            string Pair = command[1];
            string ret = DateTime.Now.ToShortDateString() + "," + DateTime.Now.ToShortTimeString() + "," + Pair + "," + platform + ","; //must be in jumps of 3
            if ((command.Length - 2) % 3 != 0)
            {
                Logger.Error("get prices amount of parameters is bad, exiting");
                return null;
            }

            try
            {
                for (int i = 2; i < command.Length; i += 3)
                {
                    if (platform.ToLower() == "binance")
                    {
                        ret += await binanceCommands.getPrice(command[i + 1], command[i], command[i + 2]);
                    }
                    else if (platform.ToLower() == "okex")
                    {
                        ret += await OkexCommands.getTodayOpenOrClose(fc, command[i], command[i + 1], command[i + 2], WhoCalledMe);
                    }
                    else
                    {
                        Logger.Error("get prices platform is bad, exiting");
                        return null;
                    }

                    ret += ",";
                }

                fc.WriteMessageToFile(WhoCalledMe, "outputGetPrices.csv", ret,true,false);
                return ret.Split(',');
            }
            catch (Exception e)
            {
                Logger.Error("getPrice failed, " + e);
            }

            return null;
        }

        public static async Task TradeExecutionMain(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            try
            {
                var exchange = m_command.GetParam(0);
                var Instrument = m_command.GetParam(1);
                var levOrNo = m_command.GetParam(2);
                var Leverage = m_command.GetParam(3);
                var Type = m_command.GetParam(4);
                var Ordertype = m_command.GetParam(5);
                var Size = m_command.GetParam(6);
                var PriceVariance = m_command.GetParam(7);
                var SweepRatio = m_command.GetParam(8);
                var Sizelimit = m_command.GetParam(9);
                var PriceLimit = m_command.GetParam(10);
                var TimeInterval = m_command.GetParam(11);
                float NetSize = 0;

                if (Ordertype == "1")
                {
                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, Type, Ordertype, Size, PriceVariance,
                        SweepRatio, Sizelimit, PriceLimit, TimeInterval);
                    return;
                }

                NetSize = float.Parse(Size);

                Ordertype = "4";

                float change = 0;
                string line = "";
                HttpClient httpClient = new HttpClient(new HttpInterceptor(client.ApiKey, client.ApiSecret, client.Passphrase, null));

                if (platform == "OKEX")
                {
                    if (exchange == "Futures" || exchange == "Swap")
                    {
                        line = (float.Parse(await OkexCommands.getPosition(fc, exchange, Instrument, client.ApiKey, client.ApiSecret,
                            client.Passphrase, WhoCalledMe, false)) + float.Parse(await OkexCommands.getPosition(fc, exchange, Instrument,
                            client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe))).ToString();
                        // 10 - 12 - 20 - this fixes a bug with positions not getting the right direction position
                    }

                    if (exchange == "Margin")
                    {
                        line = await OkexCommands.get_borrowed(fc, Instrument, client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe);
                        line = line.Split(',')[3];

                        if (float.Parse(line) < NetSize)
                        {
                            await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "1", Ordertype, (NetSize).ToString(),
                                PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                        }
                        else
                        {
                            await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "2", Ordertype, (NetSize).ToString(),
                                PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                        }

                        return;
                    }

                    if (exchange == "Spot")
                    {
                        line = await fc.GETvalidated(httpClient, $"{OkexCommands.BASEURL}api/spot/v3/accounts/{Instrument.Split('-')[0]}", WhoCalledMe);
                        line = JToken.Parse(line).SelectToken("available").ToString();
                    }
                }

                if (platform == "BINANCE")
                {
                    if (exchange == "Swap")
                    {
                        line = await binanceCommands.getSwapPosQty(Instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Futures")
                    {
                        line = await binanceCommands.getFuturesPosQty(Instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Spot")
                    {
                        line = await binanceCommands.getSpotAvail(Instrument.Split('-')[0], client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Margin")
                    {
                        line = await binanceCommands.get_borrowed(Instrument, client.ApiKey, client.ApiSecret);
                        line = line.Split(',')[3];
                    }
                }

                float posSize = float.Parse(line);
                if (posSize == 0 && exchange == "Swap" || posSize == 0 && exchange == "Futures")
                {
                    if (platform == "OKEX")
                    {
                        await OkexCommands.Set_leverageFutures(fc, exchange, Instrument, "long", Leverage, client.ApiKey, client.ApiSecret,
                            client.Passphrase, WhoCalledMe);
                        await OkexCommands.Set_leverageFutures(fc, exchange, Instrument, "short", Leverage, client.ApiKey, client.ApiSecret,
                            client.Passphrase, WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        if (exchange == "Swap")
                        {
                            await binanceCommands.changeSwapLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                        }

                        if (exchange == "Futures")
                        {
                            await binanceCommands.changeFuturesLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                        }
                    }
                }

                if (posSize > NetSize && NetSize >= 0) // for exmple 20 10 close long of 10
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = posSize - NetSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "3", Ordertype, NetSize.ToString(),
                        PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                }
                else if (posSize < NetSize && posSize >= 0) // for exmple 10 20 open long of 10
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = NetSize - posSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "1", Ordertype, NetSize.ToString(),
                        PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                }

                else if (posSize > NetSize && posSize <= 0) // for exmple -10 -20 open short of 10
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = posSize - NetSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        try
                        {
                            //Sizelimit = Convert.ToInt32(change / 2).ToString();
                        }
                        catch (Exception e)
                        {
                            Logger.Error("ExecuteCommand error with exception", e);
                            fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Trade_execute_virtual - Sizelimit ," + e.ToString());
                        }
                    }

                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "2", Ordertype, NetSize.ToString(),
                        PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                }
                else if (posSize < NetSize && NetSize <= 0) // for exmple -20 -10 close short of 10
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = NetSize - posSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "4", Ordertype, NetSize.ToString(),
                        PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                }
                else if (posSize > NetSize && NetSize < 0 && posSize > 0) // for exmple 7 -10 close long of 7 and open short of 10
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = posSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    if (platform == "BINANCE")
                    {
                        change += Math.Abs(NetSize); // 3/1/21 fixed more
                    } // 10 - 12 - 20 - fixing a bug with positions

                    if (platform == "OKEX")
                    {
                        line = await OkexCommands.getPosition(fc, exchange, Instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                            WhoCalledMe);
                        await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "3", Ordertype, 0.ToString(),
                            PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                    }

                    if (platform == "BINANCE") // fixed another bug here with it making it 0 on binance, 3/1/21
                    {
                        await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "3", Ordertype, (NetSize).ToString(),
                            PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                        return;
                    }

                    change = -NetSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    if (exchange == "Futures" || exchange == "Swap")
                    {
                        if (platform == "OKEX")
                        {
                            await OkexCommands.Set_leverageFutures(fc, exchange, Instrument, "short", Leverage, client.ApiKey, client.ApiSecret,
                                client.Passphrase, WhoCalledMe);
                        }

                        if (platform == "BINANCE")
                        {
                            if (exchange == "Swap")
                            {
                                await binanceCommands.changeSwapLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }

                            if (exchange == "Futures")
                            {
                                await binanceCommands.changeFuturesLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }
                        }
                    } // getting the appropriate starting position

                    if (platform == "OKEX")
                    {
                        line = await OkexCommands.getPosition(fc, exchange, Instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                            WhoCalledMe, false);
                    }

                    await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "2", Ordertype, NetSize.ToString(),
                        PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                }
                else if (posSize < NetSize && NetSize > 0 && posSize < 0) // for exmple -10 7 close short of 10 and open long of 7
                {
                    //1. open long; 2. open short; 3. close long; 4. close short
                    change = -posSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    if (platform == "BINANCE")
                    {
                        change += NetSize;
                    } // fixing a bug with position direction

                    if (platform == "OKEX")
                    {
                        line = await OkexCommands.getPosition(fc, exchange, Instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                            WhoCalledMe, false);
                        await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "4", Ordertype, 0.ToString(),
                            PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line, true);
                    }

                    if (platform == "BINANCE")
                    {
                        // 3/1/21 fixed a bug with it making binance twaps 0
                        await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "4", Ordertype, NetSize.ToString(),
                            PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line, true);
                        return;
                    }

                    change = NetSize;
                    if (change > 50)
                    {
                        //Sizelimit = "50";
                    }
                    else
                    {
                        //Sizelimit = Convert.ToInt32(change / 2).ToString();
                    }

                    if (exchange == "Futures" || exchange == "Swap")
                    {
                        if (platform == "OKEX")
                        {
                            await OkexCommands.Set_leverageFutures(fc, exchange, Instrument, "long", Leverage, client.ApiKey, client.ApiSecret,
                                client.Passphrase, WhoCalledMe);
                        }

                        if (platform == "BINANCE")
                        {
                            if (exchange == "Swap")
                            {
                                await binanceCommands.changeSwapLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }

                            if (exchange == "Futures")
                            {
                                await binanceCommands.changeFuturesLeverage(Instrument, Leverage, client.ApiKey, client.ApiSecret);
                            }
                        } // getting the appropriate starting position

                        if (platform == "OKEX")
                        {
                            line = await OkexCommands.getPosition(fc, exchange, Instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                                WhoCalledMe);
                        }

                        await TradeExecution(WhoCalledMe, fc, platform, client, exchange, Instrument, "1", Ordertype, NetSize.ToString(),
                            PriceVariance, SweepRatio, Sizelimit, PriceLimit, TimeInterval, line.ToString(), true);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("ExecuteCommand error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Trade_execute_virtual ," + e.ToString());
            }
        }

        public static async Task Repay(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var ins = m_command.GetParam(1);
            var coi = ins.Split('-')[0];
            var Exchange = m_command.GetParam(0);
            var qt = "1"; // = m_command.GetParam(0);
            decimal borrowed = 0;
            decimal interest = 0;

            try
            {
                if (Convert.ToDouble(qt) > 0 && Exchange != "N/A")
                {
                    if (platform == "BINANCE" && Exchange == "Swap")
                    {
                        string toRepay = "0";
                        string ret = await binanceCommands.getLoanDataSwap(client.ApiKey, client.ApiSecret);
                        string retData = await binanceCommands.getFuturesData(client.ApiKey, client.ApiSecret);
                        string availMargin = (double.Parse(JToken.Parse(retData).SelectToken("totalWalletBalance").ToString()) -
                                              double.Parse(JToken.Parse(retData).SelectToken("totalInitialMargin").ToString())).ToString();
                        if (ins == "2")
                        {
                            if (double.Parse(ret.Split(',')[0]) >= double.Parse(availMargin)) // we lost money so buying usdt to repay everything 
                            {
                                string totalAmountToBuy = ((double.Parse(ret.Split(',')[0]) - double.Parse(availMargin)) /
                                                           double.Parse(await binanceCommands.getSpotPrice("BTC-USDT"))).ToString();
                                await TradeExecution(WhoCalledMe, fc, platform, client, "Spot", "BTC-USDT", "1", "2",
                                    totalAmountToBuy, "", "", "0", "0", "0");
                                Thread.Sleep(500);
                                await binanceCommands.transfer("USDT", totalAmountToBuy, "spotSwap", client.ApiKey, client.ApiSecret);
                            }

                            toRepay = ret.Split(',')[0];

                            if (double.Parse(toRepay) != 0)
                            {
                                ret = await binanceCommands.swapRepay("USDT", "BTC", toRepay, client.ApiKey, client.ApiSecret);
                            }

                            ret = JToken.Parse(await binanceCommands.getFuturesData(client.ApiKey, client.ApiSecret)).SelectToken("totalWalletBalance")
                                .ToString();
                            if (float.Parse(ret) > 0) // we earned money,transfering it to btc 
                            {
                                await binanceCommands.transfer("USDT", ret, "swapSpot", client.ApiKey, client.ApiSecret); // make this trade execution
                                string totalAmountToBuy = (double.Parse(ret) / double.Parse(await binanceCommands.getSpotPrice("BTC-USDT"))).ToString();
                                await TradeExecution(WhoCalledMe, fc, platform, client, "Spot", "BTC-USDT", "1", "2",
                                    totalAmountToBuy, "", "", "0", "0", "0");
                            }
                        }

                        if (ins == "1") // just repaying
                        {
                            if (double.Parse(ret.Split(',')[0]) >= double.Parse(ret.Split(',')[3]))
                            {
                                toRepay = ret.Split(',')[3];
                            }
                            else
                            {
                                toRepay = ret.Split(',')[0];
                            }

                            if (double.Parse(toRepay) != 0)
                            {
                                ret = await binanceCommands.swapRepay("USDT", "BTC", toRepay, client.ApiKey, client.ApiSecret);
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            if (platform == "OKEX")
                            {
                                qt = await OkexCommands.get_borrowed(fc, ins, client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                qt = await binanceCommands.get_borrowed(ins, client.ApiKey, client.ApiSecret);
                            }

                            if (qt.Split(',')[0].Split('/')[0] == coi)
                            {
                                if (platform == "OKEX")
                                {
                                    borrowed = decimal.Parse(qt.Split(',')[2]);
                                }

                                if (platform == "BINANCE")
                                {
                                    borrowed = decimal.Parse(qt.Split(',')[3]);
                                }

                                interest = decimal.Parse(qt.Split(',')[7]);
                                qt = qt.Split(',')[3];
                            }
                            else
                            {
                                if (platform == "OKEX")
                                {
                                    borrowed = decimal.Parse(qt.Split(',')[5]);
                                }

                                if (platform == "BINANCE")
                                {
                                    borrowed = decimal.Parse(qt.Split(',')[6]);
                                }

                                interest = decimal.Parse(qt.Split(',')[8]);
                                qt = qt.Split(',')[6];
                            }
                        }

                        bool doRepay = true;
                        double MinimumPerCoin = fc.GetMinimumRequiredAmountForSpotMarginTradeClass(ins);
                        if (borrowed <= 0)
                        {
                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin repay ammount 0");
                            doRepay = false;
                        }

                        if (platform == "OKEX")
                        {
                            if (MinimumPerCoin > 0)
                            {
                                borrowed = decimal.Parse(qt) - decimal.Parse(MinimumPerCoin.ToString()) * (decimal) 1.5;
                            }
                            else
                            {
                                // this should not happen - it means we did not identify the instrumnet in the MinimumRequiredAmountForSpotMarginTradeClass class
                                borrowed = decimal.Parse(qt) * decimal.Parse("0.995");
                            }
                        }

                        if (doRepay && borrowed > 0)
                        {
                            string ret = "";
                            if (platform == "OKEX")
                            {
                                ret = await OkexCommands.margin_repay(fc, ins, coi, DecimalsHelper.ToString(borrowed), client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                ret = await binanceCommands.marginRepay(ins, coi, DecimalsHelper.ToString(borrowed), client.ApiKey, client.ApiSecret);
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin repay, " + ret);
                        }

                        if (platform == "OKEX")
                        {
                            qt = await OkexCommands.get_borrowed(fc, ins, client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe);
                        }

                        if (platform == "BINANCE")
                        {
                            qt = await binanceCommands.get_borrowed(ins, client.ApiKey, client.ApiSecret);
                        }

                        qt = qt.Split(',')[3];
                        if (float.Parse(qt) > MinimumPerCoin)
                        {
                            // on 1/10/2020 yali changed this from 2 to 4 (market to twap). 12 - 13 changing back to market
                            await TradeExecution(WhoCalledMe, fc, platform, client, "Margin", ins, "2", "2", qt, "0.005", "0.02", "0", "0",
                                "110", "0", true);
                        }
                    }
                }
                else
                {
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin repay ammount 0");
                    // don't repay 0
                }
            }
            catch (Exception e)
            {
                Logger.Error("ExecuteCommand error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Repay ," + e.ToString());
            }
        }

        public static async Task AdjustMargin(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var retur = "";
            decimal acc = 0.0025m;
            var exchange = m_command.GetParam(0);
            var instrument = m_command.GetParam(1);
            decimal targetLiq = decimal.Parse(m_command.GetParam(2));

            int x = 0;
            if (instrument.Contains("SWAP"))
            {
                return;
            }

            string posExist = "";
            decimal step = 0;
            if (exchange == "Spot" && platform == "BINANCE") // discraded
            {
            }
            else
            {
                if (exchange == "N/A")
                {
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, exchange empty moving on");
                    return;
                }


                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv",
                    "Adjust Margin : Exchange, " + exchange + ",instrument," + instrument + ",targetLiq," + targetLiq);

                if (platform == "OKEX")
                {
                    posExist = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                        WhoCalledMe);
                }

                if (platform == "BINANCE")
                {
                    if (exchange == "Futures")
                    {
                        posExist = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Swap")
                    {
                        posExist = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                    }
                }

                if (posExist == "NOPOS")
                {
                    Logger.Warn(
                        "Can't run 'Adjust Margin' command due to the fact that no open position exists");
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, no pos moving on");
                    return;
                }

                string dir = "";
                string type = "";
                string line = "";
                decimal startMargin = 0;
                decimal ratio = 0;
                decimal avgPrice = 0;
                decimal posLiq = Math.Abs(decimal.Parse(posExist));
                Logger.Debug($"posLiq is: '{posLiq}'");

                while (Math.Abs(posLiq - targetLiq) / targetLiq > acc && x < 10)
                {
                    Logger.Debug($"Attempt number {x} to adjust margin. Current liquidation price: {posLiq}, target: {targetLiq}");
                    x++;
                    try
                    {
                        if (platform == "OKEX")
                        {
                            line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                                WhoCalledMe);
                            avgPrice = decimal.Parse(await OkexCommands.getPositionCost(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                client.Passphrase, WhoCalledMe));
                        }

                        if (platform == "BINANCE")
                        {
                            if (exchange == "Futures")
                            {
                                line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                avgPrice = decimal.Parse(await binanceCommands.getFuturesPosCost(instrument, client.ApiKey, client.ApiSecret));
                            }

                            if (exchange == "Swap")
                            {
                                line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                avgPrice = decimal.Parse(await binanceCommands.getSwapPosCost(instrument, client.ApiKey, client.ApiSecret));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // empty order break
                        Logger.Error("ExecuteCommand error with exception", e);
                        break;
                    }

                    //long - (if current liq price > wanted liq price - add margin else remove margin) short is opposite
                    if (line.Contains("-"))
                    {
                        dir = "short";
                        line = (decimal.Parse(line) * -1).ToString();
                    }
                    else
                    {
                        dir = "long";
                    }

                    posLiq = decimal.Parse(line);
                    if (platform == "OKEX")
                    {
                        line = await OkexCommands.getPositionMargin(fc, exchange, instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                            WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        if (exchange == "Futures")
                        {
                            line = await binanceCommands.getFuturesPosMargin(instrument, client.ApiKey, client.ApiSecret);
                        }

                        if (exchange == "Swap")
                        {
                            line = await binanceCommands.getSwapPosMargin(instrument, client.ApiKey, client.ApiSecret);
                        }
                    }

                    step = decimal.Parse(line);
                    startMargin = step;
                    decimal margin = 0;
                    step = step / 100;
                    decimal startPosLiq = posLiq;
                    if (posLiq > targetLiq)
                    {
                        if (dir == "long")
                        {
                            type = "1"; // here i do a 2 to increase liq a little bit and find ratio then i go back with a one all the way to the end
                            // 1 reduces liq 2 increases liq in long

                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    if (step < 0.0001m)
                                    {
                                        step = decimal.Parse(line) / 20m;
                                    }

                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.changePosMarginSwap(instrument, step.ToString(), type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            margin = startMargin - step;
                            posLiq = decimal.Parse(line);
                            if (posLiq < 0)
                            {
                                posLiq *= -1;
                            }

                            ratio = (Math.Abs(startMargin - margin) / startMargin) / (Math.Abs(Math.Abs(avgPrice - startPosLiq) - Math.Abs(avgPrice - posLiq)) /
                                                                                      Math.Abs(avgPrice - startPosLiq));
                            type = "1";
                            decimal wantedLiqRatio = Math.Abs(avgPrice - targetLiq) / Math.Abs(avgPrice - posLiq);
                            step = Math.Abs(ratio * wantedLiqRatio * margin - margin * ratio);
                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, step.ToString(), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                                fc.OkexValidate(retur, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.ChangePosMarginFuturesTryLoweringAmount(
                                        instrument, step, type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.changePosMarginSwap(instrument, step.ToString(), type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, " + retur);
                        }
                        else
                        {
                            type = "2";

                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.changePosMarginSwap(instrument, step.ToString(), type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            margin = startMargin - step;
                            posLiq = decimal.Parse(line);
                            if (posLiq < 0)
                            {
                                posLiq *= -1;
                            }

                            ratio = (Math.Abs(startMargin - margin) / startMargin) / (Math.Abs(Math.Abs(avgPrice - startPosLiq) - Math.Abs(avgPrice - posLiq)) /
                                                                                      Math.Abs(avgPrice - startPosLiq));
                            type = "2";
                            decimal wantedLiqRatio = Math.Abs(avgPrice - targetLiq) / Math.Abs(avgPrice - posLiq);
                            step = Math.Abs(ratio * wantedLiqRatio * margin - margin * ratio);
                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                                fc.OkexValidate(retur, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.ChangePosMarginSwapTryLoweringAmount(
                                        instrument, step, type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, " + retur);
                        }
                    }
                    else if (posLiq < targetLiq)
                    {
                        if (dir == "long")
                        {
                            type = "2";

                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.changePosMarginSwap(instrument, step.ToString(), type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            margin = startMargin - step;
                            posLiq = decimal.Parse(line);
                            if (posLiq < 0)
                            {
                                posLiq *= -1;
                            }

                            ratio = (Math.Abs(startMargin - margin) / startMargin) / (Math.Abs(Math.Abs(avgPrice - startPosLiq) - Math.Abs(avgPrice - posLiq)) /
                                                                                      Math.Abs(avgPrice - startPosLiq));
                            type = "2";
                            decimal wantedLiqRatio = Math.Abs(avgPrice - targetLiq) / Math.Abs(avgPrice - posLiq);
                            step = Math.Abs(ratio * wantedLiqRatio * margin - margin * ratio);
                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                                fc.OkexValidate(retur, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.ChangePosMarginSwapTryLoweringAmount(
                                        instrument, step, type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, " + retur);
                        }
                        else
                        {
                            type = "1";

                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.changePosMarginSwap(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            margin = startMargin - step;
                            posLiq = decimal.Parse(line);
                            if (posLiq < 0)
                            {
                                posLiq *= -1;
                            }

                            ratio = (Math.Abs(startMargin - margin) / startMargin) / (Math.Abs(Math.Abs(avgPrice - startPosLiq) - Math.Abs(avgPrice - posLiq)) /
                                                                                      Math.Abs(avgPrice - startPosLiq));
                            type = "1";
                            decimal wantedLiqRatio = Math.Abs(avgPrice - targetLiq) / Math.Abs(avgPrice - posLiq);
                            step = Math.Abs(ratio * wantedLiqRatio * margin - margin * ratio);
                            if (platform == "OKEX")
                            {
                                retur = await OkexCommands.changeMargin(fc, exchange, instrument, dir, DecimalsHelper.ToString(step), type, client.ApiKey,
                                    client.ApiSecret, client.Passphrase);
                                Thread.Sleep(500);
                                line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret,
                                    client.Passphrase, WhoCalledMe);
                                fc.OkexValidate(retur, WhoCalledMe);
                            }

                            if (platform == "BINANCE")
                            {
                                if (exchange == "Futures")
                                {
                                    retur = await binanceCommands.changePosMarginFutures(instrument, DecimalsHelper.ToString(step), type, client.ApiKey,
                                        client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }

                                if (exchange == "Swap")
                                {
                                    retur = await binanceCommands.ChangePosMarginSwapTryLoweringAmount(
                                        instrument, step, type, client.ApiKey, client.ApiSecret);
                                    Thread.Sleep(500);
                                    line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                                }
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, " + retur);

                            //if (type == "1")
                            //{
                            //    type = "2";
                            //}
                            //else
                            //{
                            //    type = "1";
                            //}
                            //line = await OkexCommands.getPositionLiq(instrument, client.ApiKey, client.ApiSecret, client.Passphrase);
                            //posLiq = float.Parse(line);
                            //if (posLiq < 0)
                            //{
                            //    posLiq *= -1;
                            //}
                            //if (posLiq >= targetLiq)
                            //{
                            //    string ret = await OkexCommands.changeMargin(instrument, dir, step.ToString(), type, client.ApiKey, client.ApiSecret, client.Passphrase);
                            //    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change, " + ret);
                            //}
                        } // if long the pos liq has to end >= to the target and short backwards 
                    }

                    posLiq = Math.Abs(decimal.Parse(line));
                }

                if (platform == "OKEX")
                {
                    line = await OkexCommands.getPositionLiq(fc, exchange, instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                        WhoCalledMe);
                }

                if (platform == "BINANCE")
                {
                    if (exchange == "Futures")
                    {
                        line = await binanceCommands.getFuturesPosLiq(instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Swap")
                    {
                        line = await binanceCommands.getSwapPosLiq(instrument, client.ApiKey, client.ApiSecret);
                    }
                }

                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "margin change final liq price, " + line);
            }
        }

        public static async Task AdjustLtv(SingleTradingexecutionlogClassClass m_command, Client client)
        {
            double targetLtv = double.Parse(m_command.GetParam(0));
            double currentLtvRate = double.Parse((await binanceCommands.getLoanDataSwap(client.ApiKey, client.ApiSecret)).Split(',')[2]);
            double colBtc = double.Parse((await binanceCommands.getLoanDataSwap(client.ApiKey, client.ApiSecret)).Split(',')[1]);
            double btcToChange;
            if (targetLtv < currentLtvRate) // need to add ltv
            {
                btcToChange = (1 - (targetLtv / currentLtvRate)) * colBtc;
                string ha = await binanceCommands.adjustColSwap("1", "BTC", btcToChange.ToString("F5"), client.ApiKey, client.ApiSecret);
            }
            else // need to remove ltv
            {
                btcToChange = (1 - (currentLtvRate / targetLtv)) * colBtc;
                string ha = await binanceCommands.adjustColSwap("2", "BTC", btcToChange.ToString("F5"), client.ApiKey, client.ApiSecret);
            }
        }

        public static async Task Loan(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var exchange = m_command.GetParam(0);
            var inst = m_command.GetParam(1);
            var coin = m_command.GetParam(2);
            var qty = m_command.GetParam(4);
            var lev = m_command.GetParam(3);

            Logger.Info($"Starting Loan command with symbol: {inst}, coin: {coin}, quantity: {qty}, leverage: {lev}");

            if (exchange != "N/A")
            {
                try
                {
                    if (Convert.ToDouble(qty) > 0) // we dont loan 0
                    {
                        if (platform == "OKEX")
                        {
                            string retLev = await OkexCommands.Set_leverageMargin(fc, inst, lev, client.ApiKey, client.ApiSecret,
                                client.Passphrase, WhoCalledMe);
                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "loan set leverage , " + retLev);
                            string ret = await OkexCommands.margin_loan(fc, inst, coin, qty, client.ApiKey, client.ApiSecret, client.Passphrase,
                                WhoCalledMe);
                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "loan borrow, " + ret);
                        }

                        if (platform == "BINANCE")
                        {
                            string ret = "";
                            if (exchange == "Margin")
                            {
                                var maxBorrowable =
                                    await binanceCommands.MaxBorrowable(inst, coin, client.ApiKey, client.ApiSecret);

                                Logger.Debug($"Validating maximum borrowable value. Requested Value: {qty}, Max Borrowable Value: {maxBorrowable}");

                                var qtyDouble = Math.Min(Convert.ToDouble(qty), maxBorrowable);

                                Logger.Debug($"Maximum borrowable value selected: {qtyDouble}");

                                try
                                {
                                    ret = await binanceCommands.marginLoan(inst, coin,
                                        DecimalsHelper.ToString(qtyDouble), client.ApiKey, client.ApiSecret);
                                }
                                catch (BinanceErrorResponseException e)
                                {
                                    if (e.Message.Contains("Exceeding the account's maximum borrowable limit."))
                                    {
                                        var maxBorrowableForError =
                                            await binanceCommands.MaxBorrowable(inst, coin, client.ApiKey, client.ApiSecret);

                                        Logger.Error("Got 'Exceeding the account's maximum borrowable limit.' despite validating the maximum. " +
                                                     $"Requested {qtyDouble}, Original Max Borrowable was: {maxBorrowable}, " +
                                                     $"Max Borrowable value after getting this error was: {maxBorrowableForError}");
                                    }

                                    throw;
                                }
                            }

                            if (exchange == "Swap")
                            {
                                ret = await binanceCommands.swapLoan("USDT", "BTC", qty, client.ApiKey, client.ApiSecret);
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "loan borrow, " + ret);
                        }
                    }
                    else
                    {
                        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "loan ammount is 0, ");
                        // don't loan 0
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Loan command error with exception", e);
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Loan ," + e);
                }
            }
            else
            {
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "loan exchange is NA");
            }
        }

        public static async Task CancelStopLoss(string WhoCalledMe, FunctionClass fc, string platform, SingleTradingexecutionlogClassClass m_command,
            Client client)
        {
            var instrument = m_command.GetParam(2);
            var exchange = m_command.GetParam(1); // getting params from command
            var cancel = m_command.GetParam(0);

            string r = "";
            if (cancel == "Yes")
            {
                if (platform == "OKEX")
                {
                    if (exchange == "Futures" || exchange == "Swap")
                    {
                        // we call okexcommands to cancel the stop loss
                        r = await OkexCommands.cancelStopFutures(fc, exchange, instrument, client.ApiKey, client.ApiSecret, client.Passphrase,
                            WhoCalledMe);
                    }

                    if (exchange == "Spot" || exchange == "Margin")
                    {
                        // in many places in the api, spot and margin are concidered the same for some reason
                        r = await OkexCommands.cancelStopSpot(fc, instrument, client.ApiKey, client.ApiSecret, client.Passphrase, WhoCalledMe);
                    }
                }

                if (platform == "BINANCE")
                {
                    if (exchange == "Futures")
                    {
                        r = await binanceCommands.cancelOrdersFutures(instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Swap")
                    {
                        r = await binanceCommands.cancelOrdersSwap(instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Spot")
                    {
                        r = await binanceCommands.cancelOrdersSpot(instrument, client.ApiKey, client.ApiSecret);
                    }

                    if (exchange == "Margin")
                    {
                        r = await binanceCommands.cancelOrdersMargin(instrument, client.ApiKey, client.ApiSecret);
                    }
                }

                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cancel_sl," + r);
            }
            else
            {
                // we report what happend
                fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cancel_sl canceled, empty command");
            }
        }

        public static async Task TradeExecution(string WhoCalledMe, FunctionClass fc, string platform, Client client, string Exchange, string inst,
            string type, string order_type, string size, string sweep_range, string sweep_ratio, string time_interval_fill, string price_limit,
            string time_interval, string startPos = "0", bool fromVirtual = false, bool waitForTwapComp = false)
        {
            double PARAM_PriceLimitDistanceFor_TWAP = 0.4;
            double PARAM_PriceLimitDistanceFor_Market = 0.02;
            string ret = "";
            string PrintStr = "";
            string isAll = "";
            if (platform == "OKEX" && Exchange == "Futures" || platform == "OKEX" && Exchange == "Swap")
            {
                startPos = Math.Abs(float.Parse(startPos)).ToString(); // fixing problem with short
            }

            if (platform == "OKEX" && size != "All")
            {
                size = (Math.Abs(float.Parse(size))).ToString(); // okex dosnt like negative sizes
            }

            if (size == "All")
            {
                isAll = "True";
            }
            else
            {
                isAll = "False";
            }

            if (platform == "BINANCE" && order_type != "1")
            {
                order_type = "4";
            }

            try
            {
                PrintStr = "Trade Execution for :," + client.Name + ",Exchange :," + Exchange + ",Instrument," + inst + ",Type," + type + ",Ordertype," +
                           order_type + ",Size," + size + ",sweep_range ," + sweep_range + ",sweep_ratio ," + sweep_ratio + ",single_limit ," +
                           time_interval_fill + ",price_limit ," + price_limit + ",TimeInterval," + time_interval;
            }
            catch (Exception e)
            {
                Logger.Error("TradeExecution error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TradeExecution , PrintStr ," + e.ToString());
            }

            var api = client.ApiKey;
            var secret = client.ApiSecret;
            var pass = client.Passphrase;
            var accountName = client.Name;

            if (platform == "BINANCE")
            {
                if (type == "1")
                {
                    type = "4";
                }

                if (type == "2") // standardizing to okex formula
                {
                    type = "3";
                }
            }

            HttpClient Get_client = new HttpClient();
            HttpClient httpClient = new HttpClient(new HttpInterceptor(api, secret, pass, null));

            string PriceAtOrder = "";
            string price = "0";
            if (order_type == "1")
            {
                if (platform == "OKEX")
                {
                    if (Exchange == "Futures" || Exchange == "Swap")
                    {
                        string retur = await OkexCommands.cancelStopFutures(fc, Exchange, inst, api, secret, pass, WhoCalledMe);
                        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cancel_sl," + retur);
                    }

                    if (Exchange == "Spot" || Exchange == "Margin") // it cancels them together
                    {
                        string retur = await OkexCommands.cancelStopSpot(fc, inst, api, secret, pass, WhoCalledMe);
                        fc.OkexValidate(retur, WhoCalledMe);
                        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cancel_sl," + retur);
                    }
                }

                if (platform == "BINANCE")
                {
                    if (platform == "BINANCE")
                    {
                        string r = "";
                        if (Exchange == "Swap")
                        {
                            r = await binanceCommands.cancelOrdersSwap(inst, api, secret);
                        }

                        if (Exchange == "Futures")
                        {
                            r = await binanceCommands.cancelOrdersFutures(inst, api, secret);
                        }

                        if (Exchange == "Spot")
                        {
                            r = await binanceCommands.cancelOrdersSpot(inst, api, secret);
                        }

                        if (Exchange == "Margin")
                        {
                            r = await binanceCommands.cancelOrdersMargin(inst, api, secret);
                        }

                        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cancel_sl," + r);
                    }
                }
            }

            if (Exchange == "Futures" || Exchange == "Swap")
            {
                if (platform == "OKEX")
                {
                    if (Exchange == "Futures")
                    {
                        price = await fc.GETvalidated(Get_client, $"{OkexCommands.BASEURL}{OkexCommands.FUTURES_SEGMENT}/instruments/{inst}/ticker",
                            WhoCalledMe);
                    }

                    if (Exchange == "Swap")
                    {
                        price = await fc.GETvalidated(Get_client, $"{OkexCommands.BASEURL}{OkexCommands.SWAP_SEGMENT}/instruments/{inst}/ticker", WhoCalledMe);
                    }

                    JObject json = JObject.Parse(price);
                    price = json.SelectToken("last").ToString();
                }

                if (size == "All")
                {
                    if (platform == "OKEX" && type != "1")
                    {
                        size = await OkexCommands.getPosition(fc, Exchange, inst, api, secret, pass, WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        if (Exchange == "Futures")
                        {
                            size = await binanceCommands.getFuturesPosQty(inst, api, secret);
                        }

                        if (Exchange == "Swap")
                        {
                            size = await binanceCommands.getSwapPosQty(inst, api, secret);
                        }
                    }

                    if (double.Parse(size) < 0)
                    {
                        size = (double.Parse(size) * -1).ToString();
                    }
                }
            }
            else if (Exchange == "Margin")
            {
                if (platform == "OKEX")
                {
                    price = await fc.GETvalidated(Get_client, $"{OkexCommands.BASEURL}api/margin/v3/instruments/{inst}/mark_price", WhoCalledMe);
                    JObject json = JObject.Parse(price);
                    price = json.SelectToken("mark_price").ToString();
                }

                if (size == "All" && type != "1")
                {
                    if (platform == "OKEX")
                    {
                        ret = await OkexCommands.get_borrowed(fc, inst, api, secret, pass, WhoCalledMe);
                    }

                    if (platform == "BINANCE")
                    {
                        ret = await binanceCommands.get_borrowed(inst, api, secret);
                    }

                    if (order_type == "1" || type == "2")
                    {
                        size = ret.Split(',')[3];
                        if (platform == "BINANCE")
                        {
                            // TODO: consider switching to position size
                            size = ret.Split(',')[2];
                        }
                    }
                    else
                    {
                        if (type == "3")
                        {
                            size = ret.Split(',')[6];
                        }

                        if (type == "4")
                        {
                            size = ret.Split(',')[2];
                        }

                        if (type != "2" && platform == "OKEX")
                        {
                            size = (double.Parse(size) + fc.GetMinimumRequiredAmountForSpotMarginTradeClass(inst) * 10).ToString();
                        }
                    }

                    if (double.Parse(size) < 0)
                    {
                        size = (double.Parse(size) * -1).ToString();
                    }
                }
            }
            else if (Exchange == "Spot")
            {
                if (platform == "OKEX")
                {
                    price = await fc.GETvalidated(Get_client, $"{OkexCommands.BASEURL}api/spot/v3/instruments/{inst}/ticker", WhoCalledMe);
                    JObject json = JObject.Parse(price);
                    price = json.SelectToken("last").ToString();
                }

                if (size == "All")
                {
                    if (type == "3" || order_type == "1")
                    {
                        if (platform == "OKEX" && type != "1")
                        {
                            ret = await fc.GETvalidated(httpClient, $"{OkexCommands.BASEURL}api/spot/v3/accounts/{inst.Split('-')[0]}", WhoCalledMe);
                            size = JToken.Parse(ret).SelectToken("available").ToString();
                        }

                        if (platform == "BINANCE")
                        {
                            size = await binanceCommands.getSpotAvail(inst.Split('-')[0], api, secret);
                        }
                    }
                    else if (type == "4" || type == "2")
                    {
                        if (platform == "OKEX")
                        {
                            ret = await fc.GETvalidated(httpClient, $"{OkexCommands.BASEURL}api/spot/v3/accounts/{inst.Split('-')[1]}", WhoCalledMe);
                            size = JToken.Parse(ret).SelectToken("available").ToString();
                            size = JToken.Parse(ret).SelectToken("available").ToString();
                        }

                        if (platform == "BINANCE")
                        {
                            size = (float.Parse(await binanceCommands.getSpotAvail(inst.Split('-')[1], api, secret)) /
                                    float.Parse(await binanceCommands.getPrice(inst, "Spot"))).ToString();
                        }
                    }
                }
            }

            if (type == "1" && order_type == "4" || type == "1" && order_type == "2" || type == "4" && order_type == "2" || type == "4" && order_type == "4")
            {
                if (price_limit == "Pending")
                {
                    price_limit = (float.Parse(price) * 0.98).ToString();
                }
                else
                {
                    // changed to PARAM_PriceLimitDistanceFor_TWAP / _Market from 1.02 28/6/2020
                    if (order_type == "2")
                    {
                        price_limit = (float.Parse(price) * (1 + PARAM_PriceLimitDistanceFor_Market)).ToString();
                    }
                    else
                    {
                        price_limit = (float.Parse(price) * (1 + PARAM_PriceLimitDistanceFor_TWAP)).ToString();
                    }
                }
            }
            else if (type == "2" && order_type == "4" || type == "2" && order_type == "2" || type == "3" && order_type == "2" ||
                     type == "3" && order_type == "4")
            {
                if (price_limit == "Pending")
                {
                    price_limit = (float.Parse(price) * 1.02).ToString();
                }
                else
                {
                    // changed to PARAM_PriceLimitDistanceFor_TWAP / _Market from 1.02 28/6/2020
                    if (order_type == "2")
                    {
                        price_limit = (float.Parse(price) * (1 - PARAM_PriceLimitDistanceFor_Market)).ToString();
                    }
                    else
                    {
                        price_limit = (float.Parse(price) * (1 - PARAM_PriceLimitDistanceFor_TWAP)).ToString();
                    }
                }
            }
            else if (order_type == "1")
            {
            }
            else
            {
                throw new InvalidOperationException("Could Not type");
            } // removed after wanted size update

            //if ( size == "0" || size == "-0") 
            //{
            //    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "cant close a non - existing position, exiting trade execution");
            //    return;
            //}
            double sizeInUSDT = 6000;
            if (platform == "OKEX" && order_type != "1"
            ) // 4/10/2020 if spot or margin (mainly for sell avail) caculate usd value to decide if market or twap, getting the price of alt to usdt
            {
                sizeInUSDT = double.Parse(size);
                sizeInUSDT *= float.Parse(JToken
                    .Parse(await fc.GETvalidated(httpClient, $"{OkexCommands.BASEURL}api/margin/v3/instruments/{inst.Split('-')[0]}-USDT/mark_price", ""))
                    .SelectToken("mark_price").ToString());
                if (time_interval_fill == "0")
                {
                    // its from repay
                    time_interval_fill =
                        (4000 / float.Parse(JToken
                            .Parse(await fc.GETvalidated(httpClient, $"{OkexCommands.BASEURL}api/margin/v3/instruments/{inst.Split('-')[0]}-USDT/mark_price",
                                "")).SelectToken("mark_price").ToString())).ToString();
                    if (Exchange == "Futures" || Exchange == "Swap")
                    {
                        time_interval_fill = (float.Parse(time_interval_fill) / 100).ToString();
                    }
                }

                if (Exchange == "Futures" || Exchange == "Swap")
                {
                    sizeInUSDT /= 100;
                }
            }

            if (platform == "A")
            {
                if (type == "1")
                {
                    type = "BUY";
                }

                if (type == "2")
                {
                    type = "SELL";
                }

                sizeInUSDT = double.Parse(size);
                if (inst.Split('-')[1].Contains("USD") && platform == "BINANCE")
                {
                    sizeInUSDT *= double.Parse(await binanceCommands.getSpotPrice(inst.Split('-')[0] + inst.Split('-')[1]));
                }
            }

            if (platform == "BINANCE")
            {
                if (type == "4")
                {
                    type = "1";
                }

                if (type == "3")
                {
                    type = "2";
                }
            } // NEVER REMOVE 

            if (order_type == "2" && sizeInUSDT > 5000)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "market too big");
            }

            if (sweep_ratio != "Moment_TWAP" && order_type == "2" && sizeInUSDT > 15 && sizeInUSDT < 5000 ||
                sweep_ratio != "Moment_TWAP" && fromVirtual == true && sizeInUSDT < 5000 && sizeInUSDT > 15
            ) // adding conditions for market to make sure it works
            {
                if (order_type != "1")
                {
                    // If the order size is less than 21, do market order (limit with price below the current price (for long) and price above the current price for short)
                    try
                    {
                        try
                        {
                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv",
                                "Size < 11 - goint to issue Market Order, PriceAtOrder =, " + PriceAtOrder + ", " + inst + ", " + type + ", " + price + ", " +
                                size + ", " + api + ", " + secret + ", " + pass + ", " + accountName);
                            if (platform == "OKEX")
                            {
                                if (Exchange == "Margin")
                                {
                                    ret = await OkexCommands.order_marketMargin(fc, inst, type, size, api, secret, pass, WhoCalledMe);
                                }
                                else if (Exchange == "Futures" || Exchange == "Swap")
                                {
                                    ret = await OkexCommands.order_marketFutures(fc, Exchange, inst, type, price_limit, size, api, secret, pass, WhoCalledMe);
                                }
                                else if (Exchange == "Spot")
                                {
                                    ret = await OkexCommands.order_marketSpot(fc, inst, type, size, api, secret, pass, WhoCalledMe);
                                }
                            }

                            if (platform == "BINANCE")
                            {
                                if (Exchange == "Margin")
                                {
                                    ret = await binanceCommands.orderMarketMargin(inst, type, size, api, secret);
                                }
                                else if (Exchange == "Futures")
                                {
                                    ret = await binanceCommands.orderMarketFutures(inst, type, size, api, secret);
                                }
                                else if (Exchange == "Swap")
                                {
                                    ret = await binanceCommands.orderMarketSwap(inst, type, size, api, secret);
                                }
                                else if (Exchange == "Spot")
                                {
                                    ret = await binanceCommands.orderMarketSpot(inst, type, size, api, secret);
                                }
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Market Order Answer, " + ret.ToString());
                        }
                        catch (Exception e)
                        {
                            Logger.Error("TradeExecution error with exception", e);
                            fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TradeExecution , OkexCommands.order_market," + e.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("TradeExecution error with exception", e);
                        fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TradeExecution , OkexCommands.order_market 2," + e.ToString());
                    }
                }
            }
            else
            {
                try
                {
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv",
                        "Size > 10 - goint to issue TWAP Order, PriceAtOrder =," + PriceAtOrder + "," + inst + "," + type + "," + order_type + "," + size +
                        "," + sweep_range + "," + sweep_ratio + "," + time_interval_fill + "," + price_limit + "," + time_interval + "," + api + "," + secret +
                        "," + pass + "," + accountName);
                    if (sweep_ratio != "Moment_TWAP")
                    {
                        if (platform == "OKEX" && sizeInUSDT > 15)
                        {
                            if (Exchange == "Futures" || Exchange == "Swap")
                            {
                                ret = await OkexCommands.order_algoFutures(fc, Exchange, inst, type, order_type, size, sweep_range, sweep_ratio,
                                    time_interval_fill, price_limit, time_interval, api, secret, pass, WhoCalledMe);
                            }

                            if (Exchange == "Margin")
                            {
                                ret = await OkexCommands.order_algoMargin(fc, inst, type, order_type, size, sweep_range, sweep_ratio, time_interval_fill,
                                    price_limit, time_interval, api, secret, pass, WhoCalledMe);
                            }

                            if (Exchange == "Spot")
                            {
                                ret = await OkexCommands.order_algoSpot(fc, inst, type, order_type, size, sweep_range, sweep_ratio, time_interval_fill,
                                    price_limit, time_interval, api, secret, pass, WhoCalledMe);
                            }

                            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TWAP Order Answer, " + ret.ToString());
                        }

                        if (platform == "BINANCE" && size != "0")
                        {
                            if (order_type == "1")
                            {
                                if (type == "2" || type == "3")
                                {
                                    type = "SELL";
                                }

                                if (type == "1" || type == "4")
                                {
                                    type = "BUY";
                                }

                                //price_limit = (Math.Round(float.Parse(price_limit), 5)).ToString();
                                // sweep_range = (Math.Round(float.Parse(sweep_range), 5)).ToString();
                                size = (Math.Round(float.Parse(size), 2)).ToString();

                                if (Exchange == "Swap")
                                {
                                    // make size be the position size here
                                    ret = await binanceCommands.orderStopSwap(inst, type, size, sweep_range, price_limit, api, secret);
                                }

                                if (Exchange == "Futures")
                                {
                                    ret = await binanceCommands.orderStopFutures(inst, type, size, sweep_range, price_limit, api, secret);
                                }

                                if (Exchange == "Margin")
                                {
                                    // TODO: this is what we want with stop margin
                                    ret = await binanceCommands.orderStopMargin(inst, type, size, sweep_range, price_limit, api, secret);
                                }

                                if (Exchange == "Spot")
                                {
                                    ret = await binanceCommands.orderStopSpot(inst, type, size, sweep_range, price_limit, api, secret);
                                }
                            }
                        }
                    }
                    else if (order_type == "4")
                    {
                        AddTwapToFile(fc, client.Name, platform, Exchange, inst, type, size, time_interval_fill, time_interval, isAll, startPos, WhoCalledMe,
                            waitForTwapComp);
                    }
                }

                catch (Exception e)
                {
                    Logger.Error("TradeExecution error with exception", e);
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TradeExecution , OkexCommands.order_algo2," + e.ToString());
                }
            }
        }


        private static void AddTwapToFile(FunctionClass fc, string clientName, string platform, string exchange, string instrument, string type,
            string totalSize,
            string timeIntervalFill, string timeInterval, string isAll, string startPos, string whoCalledMe, bool waitForTwapComp)
        {
            if (platform == "BINANCE")
            {
                if (type == "1")
                {
                    type = "BUY"; // temporary
                }

                if (type == "2")
                {
                    type = "SELL";
                }
            }

            if (platform == "OKEX" && exchange == "Spot" || exchange == "Margin" && platform == "OKEX")
            {
                if (type == "1" || type == "4")
                {
                    type = "buy"; // temporary
                }

                if (type == "2" || type == "3")
                {
                    type = "sell";
                }
            }

            try
            {
                fc.WriteMessageToFile("MainForm_", "Twaps.csv",
                    $"{clientName},{platform},{exchange},{instrument},{type},{totalSize},{timeIntervalFill},{timeInterval},{isAll},{startPos},N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A",
                    false);
                fc.WriteMessageToFile("MainForm_", "TwapsLogs.csv",
                    $"{clientName},{platform},{exchange},{instrument},{type},{totalSize},{timeIntervalFill},{timeInterval},{isAll},{startPos},N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A");
                //if(!File.Exists(fc.GetPath() + "MainForm__Twaps.csv"))
                //{
                //    fc.WriteMessageToFile("MainForm_", "Twaps.csv", first_line,true,false);//making sure a file exists to upload twaps from
                //}
                fc.WriteMessageToFile("MainForm_", "Twaps.csv",
                    $"{clientName},{platform},{exchange},{instrument},{type},{totalSize},{timeIntervalFill},{timeInterval},{isAll},{startPos},N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A");
                //await uploadTextToS3Twap("twap_updates/MainForm__Twaps.csv", DateTime.Now.ToShortDateString() + "," + DateTime.Now.ToShortTimeString() + "," + $"{clientName},{platform},{exchange},{instrument},{type},{totalSize},{timeIntervalFill},{timeInterval},{isAll},{startPos},N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A");
                //await uploadTextToS3Twap("twap_updates/MainForm__twapLogs.csv", DateTime.Now.ToShortDateString() + "," + DateTime.Now.ToShortTimeString() + "," + $"{clientName},{platform},{exchange},{instrument},{type},{totalSize},{timeIntervalFill},{timeInterval},{isAll},{startPos},N/A,N/A,N/A,N/A,N/A,N/A,N/A,N/A");
            }
            catch (Exception e)
            {
                Logger.Error("addTwapToFile error with exception", e);
            }
        }

        private static async Task TransferMoney(Client client, FunctionClass fc, string from, string inst, string amount, string to, string fromId,
            string toId, string WhoCalledMe)
        {
            string PrintStr = "";
            try
            {
                PrintStr = "Transfer money for :," + client.Name + ",Instrument :," + inst + ",Amount," + amount + ",from," + from + ",to," + to;
            }
            catch (Exception e)
            {
                Logger.Error("TransferMoney error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TransferMoney , PrintStr ," + e.ToString());
            }

            switch (from)
            {
                case "Swap":
                    from = "9";
                    break;
                case "Spot":
                    from = "1";
                    break;
                case "Funding":
                    from = "6";
                    break;
                case "Futures":
                    from = "3";
                    break;
                case "Margin":
                    from = "5";
                    break;
                default:
                    throw new InvalidOperationException("Parse Error - can't find wallet");
            }

            switch (to)
            {
                case "Swap":
                    to = "9";
                    break;
                case "Spot":
                    to = "1";
                    break;
                case "Funding":
                    to = "6";
                    break;
                case "Futures":
                    to = "3";
                    break;
                case "Margin":
                    to = "5";
                    break;
                default:
                    throw new InvalidOperationException("Parse Error - can't find wallet");
            }

            string msg = "";
            try
            {
                Thread.Sleep(Convert.ToInt32(5000));
                msg = await OkexCommands.transfer(inst, amount, from, to, fromId, toId, client.ApiKey, client.ApiSecret, client.Passphrase, fc,
                    WhoCalledMe);
            }
            catch (Exception e)
            {
                Logger.Error("TransferMoney error with exception", e);
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "TransferMoney," + e.ToString());
            }

            fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TransferMoney," + PrintStr + ",msg :" + msg);
        }
    }
}