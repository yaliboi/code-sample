using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common
{
    public class OkexCommands
    {
        //public static Wallet wa;
        public static string BASEURL = "https://www.okex.com/";
        public static string FUTURES_SEGMENT = "api/futures/v3";
        public static string TRANSFER_SEGMENT = "api/account/v3/transfer";
        public static string SWAP_SEGMENT = "api/swap/v3";
        public static string WALLET_SEGMENT = "api/account/v3/wallet";
        public static string SPOT_WALLET = "api/spot/v3/accounts";
        public static int maxUsdtMarket = 10000;
        public static int maxContractUsdt = 100; // The max contract size in futures
        public static int DataEnteries = 200;

        public static async Task<string> transfer(string currency, string amount, string from, string to, string fromId, string toId, string api, string secret,
            string pass_phrase, FunctionClass fc, string WhoCalledMe, string type = "0")
        {
            Logger.Info($"calling okex transfer with these parameters: currency {currency} amount {amount} from {from} to {to} fromId {fromId} toId {toId}");
            Thread.Sleep(2000);
            var url = $"{BASEURL}{TRANSFER_SEGMENT}";
            Object body = new Object();
            if (fromId == "N/A" && toId == "N/A")
            {
                body = new {currency = currency, amount = amount, from = from, to = to, type = type};
            }
            else if (fromId == "N/A" && toId != "N/A")
            {
                body = new {currency = currency, amount = amount, from = from, to = to, type = type, to_instrument_id = toId};
            }
            else if (fromId != "N/A" && toId == "N/A")
            {
                body = new {currency = currency, amount = amount, from = from, to = to, type = type, instrument_id = fromId};
            }
            else if (fromId != "N/A" && toId != "N/A")
            {
                body = new {currency = currency, amount = amount, from = from, to = to, type = type, instrument_id = fromId, to_instrument_id = toId};
            }

            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                string contentStr = "";
                contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);

                return contentStr;
            }
        }

        public static async Task<string> cancelStopFutures(FunctionClass fc, string exchange, string instrument_id, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex cancelStopFutures with these parameters: exchange {exchange} instrument_id {instrument_id}");

            string[] algo_ids = new string[1];
            try
            {
                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null)))
                {
                    string j = "";
                    if (exchange == "Futures")
                    {
                        j = await fc.GETvalidated(client, $"{BASEURL}api/futures/v3/order_algo/{instrument_id}?order_type=1&status=1", WhoCalledMe, api);
                    }

                    if (exchange == "Futures")
                    {
                        j = await fc.GETvalidated(client, $"{BASEURL}api/swap/v3/order_algo/{instrument_id}?order_type=1&status=1", WhoCalledMe, api);
                    }

                    JToken json = JToken.Parse(j);
                    JToken token = json.First.SelectToken("algo_ids");
                    string id = token.ToString();
                    algo_ids[0] = id;
                }
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "cancelStopFutures," + e.ToString());
            }

            var url = $"{BASEURL}api/futures/v3/cancel_algos";
            var body = new {instrument_id = instrument_id, algo_ids = algo_ids, order_type = "1"};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> cancelStopSpot(FunctionClass fc, string instrument_id, string api, string secret, string pass_phrase,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex cancelStopSpot with these parameters: instrument_id {instrument_id}");

            string[] algo_ids = null;
            try
            {
                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null)))
                {
                    string j = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/algo/?instrument_id={instrument_id}&order_type=1&status=1", WhoCalledMe,
                        api);
                    JToken json = JToken.Parse(j);
                    json = json.First.First.First;
                    if (json == null)
                    {
                        return "{.algo_id.: 0,.error_message.: .no stop loss position.,.error_code.: .0.,.result.: false}".Replace('.', '"');
                    }

                    int orders = json.Parent.Count();
                    string[] algo_id = new string[orders];
                    for (int i = 0; i < orders; i++)
                    {
                        if (i > 0)
                        {
                            json = json.Next;
                        }

                        JToken token = json.SelectToken("algo_id");
                        string id = token.ToString();
                        algo_id[i] = id;
                    }

                    algo_ids = algo_id;
                }
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "cancelStopSpot," + e.ToString());
            }

            var url = $"{BASEURL}api/spot/v3/cancel_batch_algos";
            var body = new {instrument_id = instrument_id, algo_ids = algo_ids, order_type = "1"};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> getTodayOpenOrClose(FunctionClass fc, string exchange, string ins, string OpenOrClose, string WhoCalledMe)
        {
            Logger.Info($"calling okex getTodayOpenOrClose with these parameters: instrument_id {ins} exchange {exchange}");

            try
            {
                string line = "";
                HttpClient client = new HttpClient();
                int addHours = 2;
                if (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now.Date))
                {
                    addHours += 1;
                }

                int tries = 0;
                if (exchange == "Spot")
                {
                    string ret = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/{ins}/candles?granularity=3600", WhoCalledMe);
                    JToken token = JToken.Parse(ret).First;
                    if (OpenOrClose == "Open")
                    {
                        while (tries < 24)
                        {
                            tries += 1; // searching the last 24hrs for a close price at 9pm israel
                            if (DateTime.Parse(token.First.ToString()).Hour + addHours == 21)
                            {
                                line = token.First.Next.ToString();
                                break;
                            }

                            token = token.Next;
                        }

                        if (tries > 23)
                        {
                            line = "failed";
                        }
                    }
                    else if (OpenOrClose == "Close")
                    {
                        line = JToken.Parse(ret).First.First.Next.Next.Next.Next.ToString();
                    }
                }
                else if (exchange == "Futures")
                {
                    string ret = await fc.GETvalidated(client, $"{BASEURL}api/futures/v3/instruments/{ins}/candles?granularity=3600", WhoCalledMe);
                    if (OpenOrClose == "Open")
                    {
                        JToken token = JToken.Parse(ret).First;
                        if (OpenOrClose == "Open")
                        {
                            while (tries < 24)
                            {
                                tries += 1; // searching the last 24hrs for a close price at 9pm israel
                                if (DateTime.Parse(token.First.ToString()).Hour + addHours == 21)
                                {
                                    line = token.First.Next.ToString();
                                    break;
                                }

                                token = token.Next;
                            }

                            if (tries > 23)
                            {
                                line = "failed";
                            }
                        }
                    }
                    else if (OpenOrClose == "Close")
                    {
                        line = JToken.Parse(ret).First.First.Next.Next.Next.Next.ToString();
                    }
                }
                else if (exchange == "Swap")
                {
                    string ret = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/instruments/{ins}/candles?granularity=3600", WhoCalledMe);
                    if (OpenOrClose == "Open")
                    {
                        JToken token = JToken.Parse(ret).First;
                        if (OpenOrClose == "Open")
                        {
                            while (tries < 24)
                            {
                                tries += 1; // searching the last 24hrs for a close price at 9pm israel
                                if (DateTime.Parse(token.First.ToString()).Hour + addHours == 21)
                                {
                                    line = token.First.Next.ToString();
                                    break;
                                }

                                token = token.Next;
                            }

                            if (tries > 23)
                            {
                                line = "failed";
                            }
                        }
                    }
                    else if (OpenOrClose == "Close")
                    {
                        line = JToken.Parse(ret).First.First.Next.Next.Next.Next.ToString();
                    }
                }

                return line;
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "getTodayOpen," + e.ToString());
            }

            return "";
        }

        public static async Task<List<Candle>> getAllCandles(FunctionClass fc, string time, string symbol, string WhoCalledMe)
        {
            Logger.Info($"calling okex getAllCandles with these parameters: instrument_id {symbol} time {time}");

            var url = $"{BASEURL}api/spot/v3/instruments/{symbol}/candles?granularity={time}";
            if (symbol.Split('-').Length > 2)
            {
                url = $"{BASEURL}api/futures/v3/instruments/{symbol}/candles?granularity=3600";
            }

            try
            {
                //string line = "";
                HttpClient client = new HttpClient();
                string ret = await fc.GETvalidated(client, url, WhoCalledMe);
                decimal open;
                decimal high;
                decimal low;
                decimal close;
                string date = "";
                decimal vol;
                var res = new List<Candle>();
                JToken token = JToken.Parse(ret).First;

                for (int i = 0; i < JToken.Parse(ret).ToList().Count; i++)
                {
                    date = token.First.ToString();
                    open = (decimal) token.First.Next;
                    high = (decimal) token.First.Next.Next;
                    low = (decimal) token.First.Next.Next.Next;
                    close = (decimal) token.First.Next.Next.Next.Next;
                    vol = (decimal) token.First.Next.Next.Next.Next.Next;
                    token = token.Next; // need to test this

                    res.Add(new Candle
                    {
                        Date = DateTime.Parse(date),
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = vol,
                    });
                }

                return res;
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get24hr," + e);
            }

            return null;
        }

        public static async Task<string> Get_max(string subWallet, string currency, string inst, string api, string secret, string pass, FunctionClass fc,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex Get_max with these parameters: subWallet {subWallet} currency {currency} inst {inst}");

            string ammount = "";
            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            try
            {
                if (subWallet == "Margin")
                {
                    string result = await get_borrowed(fc, inst, api, secret, pass, WhoCalledMe);


                    if (result.Split(',')[4] == currency)
                    {
                        ammount = result.Split(',')[10];
                    }
                    else
                    {
                        ammount = result.Split(',')[9];
                    }

                    ammount = (double.Parse(ammount) - 0.01).ToString();
                }

                if (subWallet == "Futures" || subWallet == "Swap")
                {
                    string result = "";
                    if (subWallet == "Futures")
                    {
                        result = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/accounts", WhoCalledMe, api);
                    }

                    if (subWallet == "Swap")
                    {
                        result = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/accounts", WhoCalledMe, api);
                    }

                    JObject json = JObject.Parse(result);
                    JToken token = json.First.First;
                    string currenc = "";
                    if (inst == "N/A")
                    {
                        if (token.First != null)
                        {
                            if (subWallet == "Futures")
                            {
                                try
                                {
                                    token = token.SelectToken(currency.ToLower());
                                }
                                catch
                                {
                                    token = token.SelectToken(inst.Split('-')[0].ToLower() + "-" + inst.Split('-')[1].ToLower());
                                }

                                if (token == null)
                                {
                                    token = json.First.First.First.First;
                                }

                                token = token.SelectToken("total_avail_balance");
                            }

                            if (subWallet == "Swap")
                            {
                                token = token.First;
                                for (int i = 0; i < token.Parent.Count(); i++)
                                {
                                    if (token.SelectToken("currency").ToString() == currency)
                                    {
                                        token = token.SelectToken("max_withdraw").ToString();
                                        break;
                                    }

                                    token = token.Next;
                                }
                            }
                        }
                    }
                    else
                    {
                        token = token.SelectToken((inst.Split('-')[0] + "-" + inst.Split('-')[1]).ToLower());
                        if (token == null)
                        {
                            return "nothing in instrument";
                        }

                        token = token.SelectToken("total_avail_balance");
                    }

                    if (token == null)
                    {
                        ammount = "0";
                    }
                    else
                    {
                        ammount = token.ToString();
                    }
                }

                if (subWallet == "Spot")
                {
                    string result = await fc.GETvalidated(client, $"{BASEURL}{SPOT_WALLET}/{currency}", WhoCalledMe, api);
                    JToken json = JToken.Parse(result);
                    ammount = json.SelectToken("balance").ToString();
                }

                if (subWallet == "Funding")
                {
                    string result = await fc.GETvalidated(client, $"{BASEURL}api/account/v3/wallet/{currency}", WhoCalledMe, api);
                    JToken json = JToken.Parse(result);
                    ammount = json.SelectToken("balance").ToString();
                }
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get_max," + e.ToString());
            }

            if (ammount == null)
            {
                ammount = "0";
            }

            return ammount;
        }

        public static async Task<string> get_margin_total(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            Logger.Info($"calling okex get_margin_total");

            while (true)
            {
                Thread.Sleep(3000);
                try
                {
                    var client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
                    string result = await fc.GETvalidated(client, $"{BASEURL}api/account/v3/asset-valuation?account_type=5", WhoCalledMe, api);
                    return JObject.Parse(result).SelectToken("balance").ToString();
                }
                catch (Exception e)
                {
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get_margin_total ," + e.ToString());
                }
            }
        }

        public static async Task<string> getPosition(FunctionClass fc, string exchange, string inst, string api, string secret, string pass, string WhoCalledMe,
            bool isLong = true)
        {
            Logger.Info($"calling okex getPosition with these parameters: exchange {exchange} inst {inst}");

            string line = "";

            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            string res = "";
            if (exchange == "Futures")
            {
                res = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/{inst}/position", WhoCalledMe, api);
            }

            if (exchange == "Swap")
            {
                res = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/{inst}/position", WhoCalledMe, api);
            }

            JToken token = JToken.Parse(res);
            token = token.SelectToken("holding").First;
            if (token == null)
            {
                return "0";
            }

            string lon = "";
            string shor = "";
            if (exchange == "Futures")
            {
                lon = token.SelectToken("long_avail_qty").ToString();
                shor = token.SelectToken("short_avail_qty").ToString();
            }

            if (exchange == "Swap")
            {
                lon = token.SelectToken("position").ToString();
                shor = lon;
                if (token.SelectToken("side").ToString() == "short")
                {
                    lon = "0";
                }
            }

            if (isLong)
            {
                line = $"{lon}";
            }
            else
            {
                line = $"-{shor}";
            }

            return line;
        }

        public static async Task<string> getPositionCost(FunctionClass fc, string exchange, string inst, string api, string secret, string pass,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex getPositionCost with these parameters: exchange {exchange} inst {inst}");

            string line = "";
            var res = "";
            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            try
            {
                if (exchange == "Futures")
                {
                    res = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/{inst}/position", WhoCalledMe, api);
                }

                if (exchange == "Swap")
                {
                    res = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/{inst}/position", WhoCalledMe, api);
                }
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "getPositionLiq," + e.ToString());
            }

            JToken token = JToken.Parse(res);
            token = token.SelectToken("holding").First;
            if (token == null)
            {
                return "NOPOS";
            }

            string lon = "";
            string shor = "";
            string shortCost = "";
            string longCost = "";
            if (exchange == "Futures")
            {
                lon = token.SelectToken("long_liqui_price").ToString();
                shor = token.SelectToken("short_liqui_price").ToString();
                shortCost = token.SelectToken("short_avg_cost").ToString();
                longCost = token.SelectToken("long_avg_cost").ToString();
            }

            if (exchange == "Swap")
            {
                longCost = token.SelectToken("avg_cost").ToString();
                lon = token.SelectToken("liquidation_price").ToString();
            }

            if (float.Parse(lon) > 0)
            {
                line = $"{longCost}";
            }
            else if (float.Parse(shor) > 0)
            {
                line = $"{shortCost}";
            }
            else
            {
                line = "NOPOS";
            }

            return line;
        }

        public static async Task<string> getPositionLiq(FunctionClass fc, string exchange, string inst, string api, string secret, string pass,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex getPositionLiq with these parameters: exchange {exchange} inst {inst}");

            string line = "";
            var res = "";
            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            try
            {
                if (exchange == "Futures")
                {
                    res = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/{inst}/position", WhoCalledMe, api);
                }

                if (exchange == "Swap")
                {
                    res = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/{inst}/position", WhoCalledMe, api);
                }
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "getPositionLiq," + e.ToString());
            }

            JToken token = JToken.Parse(res);
            token = token.SelectToken("holding").First;
            if (token == null)
            {
                return "NOPOS";
            }

            string lon = "";
            string shor = "";
            string shortCost = "";
            string longCost = "";
            if (exchange == "Futures")
            {
                lon = token.SelectToken("long_liqui_price").ToString();
                shor = token.SelectToken("short_liqui_price").ToString();
            }

            if (exchange == "Swap")
            {
                longCost = token.SelectToken("avg_cost").ToString();
                lon = token.SelectToken("liquidation_price").ToString();
            }

            if (float.Parse(lon) > 0)
            {
                line = $"{lon}";
            }
            else if (float.Parse(shor) > 0)
            {
                line = $"-{shor}";
            }
            else
            {
                line = "NOPOS";
            }

            return line;
        }

        public static async Task<string> getPositionMargin(FunctionClass fc, string exchange, string inst, string api, string secret, string pass,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex getPositionMargin with these parameters: exchange {exchange} inst {inst}");

            string line = "";
            string res = "";
            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            if (exchange == "Futures")
            {
                res = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/{inst}/position", WhoCalledMe, api);
            }

            if (exchange == "Swap")
            {
                res = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/{inst}/position", WhoCalledMe, api);
            }

            JToken token = JToken.Parse(res);
            token = token.SelectToken("holding").First;
            if (token == null)
            {
                return "NOPOS";
            }

            string lon = "";
            string shor = "";
            if (exchange == "Futures")
            {
                lon = token.SelectToken("long_margin").ToString();
                shor = token.SelectToken("short_margin").ToString();
            }

            if (exchange == "Swap")
            {
                lon = token.SelectToken("margin").ToString();
            }

            if (float.Parse(lon) > 0)
            {
                line = $"{lon}";
            }
            else if (float.Parse(shor) > 0)
            {
                line = $"{shor}";
            }
            else
            {
                line = "NOPOS";
            }

            return line;
        }

        public static async Task<string[]> Order_data(FunctionClass fc, string exchange, string api, string secret, string pass, string WhoCalledMe)
        {
            Logger.Info($"calling okex Order_data with these parameters: exchange {exchange}");

            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            string result = "";
            if (exchange == "Futures")
            {
                result = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/position", WhoCalledMe, api);
            }

            if (exchange == "Swap")
            {
                result = await fc.GETvalidated(client, $"{BASEURL}{SWAP_SEGMENT}/position", WhoCalledMe, api);
            }

            JObject json = JObject.Parse(result);
            JToken holding = json.SelectToken("holding");
            JToken position = holding.First.First;
            int len = position.Parent.Count;
            bool shor = false;
            string lon;
            string[] lines = new string[len];
            string pnl, margin, liq, lev, inst, qty;
            for (int i = 0; i < len; i++)
            {
                if (i > 0)
                {
                    position = position.Next;
                }

                shor = false;
                lon = position.SelectToken("long_qty").ToString();
                if (lon == "0")
                {
                    shor = true;
                }

                inst = position.SelectToken("instrument_id").ToString();
                if (shor == false)
                {
                    qty = lon;
                    margin = position.SelectToken("long_margin").ToString();
                    liq = position.SelectToken("long_liqui_price").ToString();
                    lev = position.SelectToken("long_leverage").ToString();
                    pnl = position.SelectToken("long_pnl").ToString();
                }
                else
                {
                    qty = position.SelectToken("short_qty").ToString();
                    margin = position.SelectToken("short_margin").ToString();
                    liq = position.SelectToken("short_liqui_price").ToString();
                    lev = position.SelectToken("short_leverage").ToString();
                    pnl = position.SelectToken("short_pnl").ToString();
                }

                string resultline = ($"{inst},{qty},{margin},{lev},{liq},{pnl},");
                lines[i] = resultline;
            }

            return lines;
        }

        public static async Task Set_leverageFutures(FunctionClass fc, string Exchange, string inst, string direction, string leverage, string api,
            string secret, string pass, string WhoCalledMe)
        {
            Logger.Info($"calling okex Set_leverageFutures with these parameters: exchange {Exchange} inst {inst} direction {direction} leverage {leverage}");

            //inst = inst.Substring(0, 3) + "-" + inst.Substring(3, 3) + "-" + year + inst.Substring(6, inst.Length - 6);
            string underlying = inst;
            if (Exchange == "Futures")
            {
                underlying = inst.Split('-')[0] + "-" + inst.Split('-')[1];
            }

            string results = await set_leverage(fc, Exchange, inst, direction, leverage, underlying, api, secret, pass, WhoCalledMe);
            Console.WriteLine(results);
        }

        public static async Task<string> Set_leverageMargin(FunctionClass fc, string instrument_id, string leverage, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex Set_leverageMargin with these parameters: inst {instrument_id} leverage {leverage}");

            if (leverage == "N/A")
            {
                return "leverage is NA";
            }

            var url = $"{BASEURL}api/margin/v3/accounts/{instrument_id}/leverage";
            var body = new {instrument_id = instrument_id, leverage = leverage};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        //api/futures/v3/positon/margin{"instrument_id":"BTC-USDT-200626","direction":"long,"amount":"1","type":"1"}
        public static async Task<string> changeMargin(FunctionClass fc, string exchange, string instrument_id, string direction, string amount, string type,
            string api, string secret, string pass_phrase)
        {
            Logger.Info(
                $"calling okex changeMargin with these parameters: exchange {exchange} inst {instrument_id} direction {direction} amount {amount} type {type}");

            string url = "";
            if (exchange == "Futures")
            {
                url = $"{BASEURL}api/futures/v3/position/margin";
            }

            if (exchange == "Swap")
            {
                url = $"{BASEURL}api/swap/v3/position/margin";
            }

            var body = new {instrument_id = instrument_id, direction = direction, amount = amount, type = type};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, "");
                if (contentStr.Contains("Max/min margin limit"))
                {
                    try
                    {
                        Logger.Error(
                            "changeMargin said balance insufficent, trying to take some more balance and try again, with a lower amount too beacuse the problem might also be beacuse amount to big unrelated to transfering like if reducing position or something");

                        await transfer(instrument_id.Split('-')[0], (double.Parse(amount) * 0.95).ToString(), "6", "3", "N/A", instrument_id, api, secret,
                            pass_phrase, fc, "");
                        body = new {instrument_id = instrument_id, direction = direction, amount = (double.Parse(amount) * 0.95).ToString(), type = type};
                        bodyStr = JsonConvert.SerializeObject(body);
                        contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, "");
                    }
                    catch
                    {
                        Logger.Error("changeMargin failed again, giving up");
                    }
                }

                return contentStr;
            }
        }

        public static async Task<string[]> Get_leverageFutures(FunctionClass fc, string api, string secret, string pass_phrase, string underlying,
            string WhoCalledMe)
        {
            Logger.Info($"calling okex Get_leverageFutures with these parameters: underlying {underlying}");

            var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null));
            string res = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/accounts/{underlying}/leverage", WhoCalledMe, api);
            JObject json = JObject.Parse(res);
            JToken small = json.First.First;
            string[] lines = new string[json.Count];
            for (int i = 0; i < json.Count; i++)
            {
                if (i > 0)
                {
                    small = small.Parent.Next.First;
                }

                if (small.Count() > 0)
                {
                    string inst = small.Parent.ToString().Split('"')[1];
                    string lon = small.SelectToken("long_leverage").ToString();
                    string shor = small.SelectToken("short_leverage").ToString();
                    lines[i] = ($"{inst},{lon},{shor}");
                }
            }

            lines = lines.Where(x => !string.IsNullOrEmpty(x)).ToArray();
            return lines;
        }

        public static async Task<string> set_leverage(FunctionClass fc, string exchange, string instrument_id, string direction, string leverage,
            string underlying, string api, string secret, string pass_phrase, string WhoCalledMe)
        {
            Logger.Info(
                $"calling okex set_leverage with these parameters: exchange {exchange} inst {instrument_id} direction {direction} leverage {leverage} underlying {underlying}");

            if (leverage == "N/A")
            {
                return "leverage is NA";
            }

            string url = "";
            object body = null;
            if (exchange == "Futures")
            {
                url = $"{BASEURL}{FUTURES_SEGMENT}/accounts/{underlying}/leverage";
                body = new {instrument_id = instrument_id, direction = direction, leverage = leverage, underlying = underlying};
            }

            if (exchange == "Swap")
            {
                url = $"{BASEURL}{SWAP_SEGMENT}/accounts/{underlying}/leverage";
                if (direction == "long")
                {
                    direction = "1";
                }

                if (direction == "short")
                {
                    direction = "2";
                }

                body = new {instrument_id = instrument_id, side = direction, leverage = leverage, underlying = underlying};
            }

            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> getClientTotal(FunctionClass fc, string api, string secret, string pass, string name, string WhoCalledMe,
            string baseCurrency = "BTC")
        {
            Logger.Info($"calling okex getClientTotal with these parameters: name {name}");

            string total = "";
            string priceBTC = "";
            HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));

            try
            {
                priceBTC = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/BTC-USDT/ticker", WhoCalledMe, api);
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get total 1," + e.ToString());
            }

            decimal totalBase = 0;
            if (baseCurrency == "BTC")
            {
                try
                {
                    total = await fc.GETvalidated(client, $"{BASEURL}api/account/v3/asset-valuation", WhoCalledMe);
                }
                catch (Exception e)
                {
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get total 2," + e.ToString());
                }

                if (total == "Too Many Requests")
                {
                    Thread.Sleep(15000);
                    throw new Exception("Too Many Requests");
                }

                totalBase = decimal.Parse(JObject.Parse(total).SelectToken("balance").ToString());
            }
            else
            {
                totalBase = decimal.Parse(JToken
                    .Parse(await fc.GETvalidated(client, $"{OkexCommands.BASEURL}api/account/v3/wallet/{baseCurrency}", WhoCalledMe)).First
                    .SelectToken("balance").ToString());
                decimal totalSpot = decimal.Parse(JToken
                    .Parse(await fc.GETvalidated(client, $"{OkexCommands.BASEURL}api/spot/v3/accounts/{baseCurrency}", WhoCalledMe)).SelectToken("balance")
                    .ToString());
                decimal totalMargin = decimal.Parse(JToken
                    .Parse(await fc.GETvalidated(client, $"{OkexCommands.BASEURL}api/margin/v3/accounts/{baseCurrency}-USDT", WhoCalledMe))
                    .SelectToken($"currency:{baseCurrency}").SelectToken("balance").ToString());
                decimal totalFutures = decimal.Parse(JToken
                    .Parse(await fc.GETvalidated(client, $"{OkexCommands.BASEURL}api/futures/v3/accounts/{baseCurrency}-USD", WhoCalledMe))
                    .SelectToken("equity").ToString());
                totalBase += totalSpot + totalMargin + totalFutures;
            }

            decimal totalUSD = decimal.Parse(JToken.Parse(priceBTC).SelectToken("last").ToString()) * totalBase;
            string ret = DecimalsHelper.ToString(totalBase) + "," + totalUSD;
            Thread.Sleep(2000);
            return DecimalsHelper.ToString(totalBase);
        }

        public static async Task<string[]> get_borrowed7(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            Logger.Info($"calling okex get_borrowed7");

            string[] lines = new string[15];
            lines[0] = await get_borrowed(fc, "BTC-USDT", api, secret, pass, WhoCalledMe);
            lines[1] = await get_borrowed(fc, "LTC-USDT", api, secret, pass, WhoCalledMe);
            lines[2] = await get_borrowed(fc, "ETH-USDT", api, secret, pass, WhoCalledMe);
            lines[3] = await get_borrowed(fc, "ETC-USDT", api, secret, pass, WhoCalledMe);
            lines[4] = await get_borrowed(fc, "XRP-USDT", api, secret, pass, WhoCalledMe);
            lines[5] = await get_borrowed(fc, "EOS-USDT", api, secret, pass, WhoCalledMe);
            lines[6] = await get_borrowed(fc, "BCH-USDT", api, secret, pass, WhoCalledMe);
            lines[7] = await get_borrowed(fc, "BSV-USDT", api, secret, pass, WhoCalledMe);
            lines[8] = await get_borrowed(fc, "LTC-BTC", api, secret, pass, WhoCalledMe);
            lines[9] = await get_borrowed(fc, "ETH-BTC", api, secret, pass, WhoCalledMe);
            lines[10] = await get_borrowed(fc, "ETC-BTC", api, secret, pass, WhoCalledMe);
            lines[11] = await get_borrowed(fc, "XRP-BTC", api, secret, pass, WhoCalledMe);
            lines[12] = await get_borrowed(fc, "EOS-BTC", api, secret, pass, WhoCalledMe);
            lines[13] = await get_borrowed(fc, "BCH-BTC", api, secret, pass, WhoCalledMe);
            lines[14] = await get_borrowed(fc, "BSV-BTC", api, secret, pass, WhoCalledMe);
            return lines;
        }

        public static async Task<string> get_borrowed(FunctionClass fc, string inst, string api, string secret, string pass, string WhoCalledMe)
        {
            Logger.Info($"calling okex get_borrowed with these parameters: inst {inst}");

            string result = "";
            var client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            try
            {
                result = await fc.GETvalidated(client, $"{BASEURL}api/margin/v3/accounts/{inst}", WhoCalledMe, api);
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile("MainForm", "Exception Log.csv", "get_borrowed," + e.ToString());
            }

            JToken json = JToken.Parse(result);
            JToken sub = json.First.First;
            string currency1 = json.First.Path.Split(':')[1];
            string balance1 = sub.SelectToken("borrowed").ToString();
            string borrowed1 = sub.SelectToken("available").ToString();
            string qty1 = sub.SelectToken("balance").ToString();
            string interest1 = sub.SelectToken("lending_fee").ToString();
            string max1 = sub.SelectToken("can_withdraw").ToString();
            sub = sub.Parent.Next.First;
            string currency2 = json.First.Next.Path.Split(':')[1];
            string qty2 = sub.SelectToken("balance").ToString();
            string balance2 = sub.SelectToken("borrowed").ToString();
            string borrowed2 = sub.SelectToken("available").ToString();
            string interest2 = sub.SelectToken("lending_fee").ToString();
            string max2 = sub.SelectToken("can_withdraw").ToString();
            string line = "";
            if (currency2 != "USDT" && currency2 != "BTC" || currency2 == "BTC" && currency1 == "USDT")
            {
                // sometimes the server brings back the values switched so we switch them back
                line =
                    $"{currency2}/{currency1},{currency2},{balance2},{borrowed2},{currency1},{balance1},{borrowed1},{interest2},{interest1},{max2},{max1},{qty2},{qty1}";
            }
            else
            {
                line =
                    $"{currency1}/{currency2},{currency1},{balance1},{borrowed1},{currency2},{balance2},{borrowed2},{interest1},{interest2},{max1},{max2},{qty1},{qty2}";
            }

            return line;
        }
        //public static async Task OrderMargin(FunctionClass fc, string inst, string type, string order_type, string size, string sweep_range, string sweep_ratio, string single_limit, string price_limit, string time_interval, string api, string secret, string pass, WriteToFile WTF, string WhoCalledMe)
        //{

        //    //when ordering twap total ammount(size) is in the instrument not usd
        //    // single limit is in the insturment
        //    //price limit is in usd

        //    //when buying market ammount is in usd
        //    //when selling market ammount is in instrument

        //    if (float.Parse(size) < 21) //  || int.Parse(single_limit) < 11)
        //    {
        //        // If the order size is less than 10, do market order (limit with price below the current price (for long) and price above the current price for short)

        //        try
        //        {
        //            //WTF.LogMessageToFile("/2 TWAP and Market Order Activity.csv", "Size < 11 - goint to issue Market Order, PriceAtOrder =, " + PriceAtOrder + "," + inst + "," + type + "," + price + "," + size + "," + api + "," + secret + "," + pass + "," + accountName);
        //            string ret = await order_marketMargin(fc, inst, type, size, api, secret, pass);
        //            //WTF.LogMessageToFile("/2 TWAP and Market Order Activity.csv", "Market Order Answer, " + ret.ToString());
        //            Console.WriteLine(ret);
        //        }
        //        catch (Exception e)
        //        {
        //            fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "OrderMargin 1," + e.ToString());
        //        }
        //    }


        //    else
        //    {
        //        try
        //        {
        //            //WTF.LogMessageToFile("/2 TWAP and Market Order Activity.csv", "Size > 10 - goint to issue TWAP Order, PriceAtOrder =," + PriceAtOrder + "," + inst + "," + type + "," + order_type + "," + size + "," + sweep_range + "," + sweep_ratio + "," + single_limit + "," + price_limit + "," + time_interval + "," + api + "," + secret + "," + pass + "," + accountName);
        //            string ret = await order_algoMargin(m_mainForm, fc,inst, type, order_type, size, sweep_range, sweep_ratio, single_limit, price_limit, time_interval, api, secret, pass, WhoCalledMe);
        //            Console.WriteLine(ret);
        //            //WTF.LogMessageToFile("/2 TWAP and Market Order Activity.csv", "TWAP Order Answer, " + ret.ToString());
        //        }
        //        catch (Exception e)
        //        {
        //            fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "OrderMargin 2," + e.ToString());
        //        }
        //    }
        //}
        public static async Task<string> order_marketFutures(FunctionClass fc, string exchange, string instrument_id, string type, string price, string size,
            string api, string secret, string pass_phrase, string WhoCalledMe)
        {
            Logger.Info(
                $"calling okex order_marketFutures with these parameters: instrument_id {instrument_id} exchange {exchange} type {type} price {price} size {size}");

            int usdtValue = 0;
            var url = $"{BASEURL}{FUTURES_SEGMENT}/order";
            using (var Get_client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null)))
            {
                if (exchange == "Futures")
                {
                    url = $"{BASEURL}{FUTURES_SEGMENT}/order";
                }

                if (exchange == "Swap")
                {
                    url = $"{BASEURL}{SWAP_SEGMENT}/order";
                }

                int Local_maxContractUsdt = maxContractUsdt;
                if (instrument_id.Contains("BTC-USD"))
                {
                    Local_maxContractUsdt = 100;
                }

                if (instrument_id.Contains("BTC-USDT"))
                {
                    Local_maxContractUsdt = 92;
                }

                if (instrument_id.Contains("ETH-USDT"))
                {
                    Local_maxContractUsdt = 22;
                }

                if (instrument_id.Contains("LTC-USDT"))
                {
                    Local_maxContractUsdt = 41;
                }

                if (instrument_id.Contains("ETC-USDT"))
                {
                    Local_maxContractUsdt = 57;
                }

                if (instrument_id.Contains("XRP-USDT"))
                {
                    Local_maxContractUsdt = 17;
                }

                if (instrument_id.Contains("EOS-USDT"))
                {
                    Local_maxContractUsdt = 24;
                }

                if (instrument_id.Contains("BCH-USDT"))
                {
                    Local_maxContractUsdt = 22;
                }

                if (instrument_id.Contains("BSV-USDT"))
                {
                    Local_maxContractUsdt = 157;
                }

                usdtValue = Local_maxContractUsdt * int.Parse(size);
            }

            if (usdtValue > maxUsdtMarket) // work with roy
            {
                // big market order probably make sure we allways have stop loss breakpoint break point
                fc.SetStopAllAutoRun(true);
                Thread.Sleep(10000);
            }

            var body = new {instrument_id = instrument_id, type = type, size = size, order_type = "4"};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> margin_loan(FunctionClass fc, string instrument_id, string currency, string amount, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex margin_loan with these parameters: instrument_id {instrument_id} currency {currency} amount {amount}");

            var url = $"{BASEURL}api/margin/v3/accounts/borrow";
            var body = new {instrument_id = instrument_id, currency = currency, amount = amount};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> margin_repay(FunctionClass fc, string instrument_id, string currency, string amount, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex margin_repay with these parameters: instrument_id {instrument_id} currency {currency} amount {amount}");

            var url = $"{BASEURL}api/margin/v3/accounts/repayment";
            var body = new {instrument_id = instrument_id, currency = currency, amount = amount};
            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                return contentStr;
            }
        }

        public static async Task<string> order_marketMargin(FunctionClass fc, string instrument_id, string type, string size, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex order_marketMargin with these parameters: instrument_id {instrument_id} type {type} size {size}");

            var url = $"{BASEURL}api/margin/v3/orders";
            if (type == "1" || type == "4")
            {
                type = "buy";
            }
            else if (type == "3" || type == "2")
            {
                type = "sell";
            }

            string alt = "";
            string usdtValue = "0";
            using (var client = new HttpClient())
            {
                try
                {
                    string altJson = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/{instrument_id.Split('-')[0]}-USDT/ticker", WhoCalledMe,
                        api);
                    JToken altToken = JToken.Parse(altJson);
                    altToken = altToken.SelectToken("last");
                    usdtValue = altToken.ToString();
                    altJson = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/{instrument_id}/ticker", WhoCalledMe, api);
                    altToken = JToken.Parse(altJson);
                    altToken = altToken.SelectToken("last");
                    alt = altToken.ToString();
                }
                catch (Exception e)
                {
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "order_marketMargin ," + e.ToString());
                }
            }

            if (double.Parse(usdtValue) * double.Parse(size) > maxUsdtMarket)


            {
                // market size probably too big
            }

            if (type == "buy")
            {
                var body = new {instrument_id = instrument_id, side = type, margin_trading = "2", size = size, price = (float.Parse(alt) * 1.01).ToString()};
                var bodyStr = JsonConvert.SerializeObject(body);
                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
                {
                    var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                    return contentStr;
                }
            }
            else
            {
                var body = new {instrument_id = instrument_id, side = type, margin_trading = "2", size = size, price = (float.Parse(alt) * 0.99).ToString()};
                var bodyStr = JsonConvert.SerializeObject(body);
                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
                {
                    var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                    return contentStr;
                }
            }
        }

        public static async Task<string> order_marketSpot(FunctionClass fc, string instrument_id, string type, string size, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info($"calling okex order_marketSpot with these parameters: instrument_id {instrument_id} type {type} size {size}");

            var url = $"{BASEURL}api/spot/v3/orders";
            if (type == "1" || type == "4")
            {
                type = "buy";
            }
            else if (type == "2" || type == "3")
            {
                type = "sell";
            }

            string alt = "";
            string usdtValue = "0";
            using (var client = new HttpClient())
            {
                string altJson =
                    await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/{instrument_id.Split('-')[0]}-USDT/ticker",
                        WhoCalledMe, api); // change 06 04 2020
                JToken altToken = JToken.Parse(altJson);
                altToken = altToken.SelectToken("last");
                usdtValue = altToken.ToString();
                altJson = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/{instrument_id}/ticker", WhoCalledMe, api);
                altToken = JToken.Parse(altJson);
                altToken = altToken.SelectToken("last");
                alt = altToken.ToString();
            }

            if (double.Parse(usdtValue) * double.Parse(size) > maxUsdtMarket)
            {
                // market size probably too big
            }

            if (type == "buy")
            {
                var body = new {instrument_id = instrument_id, side = type, size = size, price = (float.Parse(alt) * 1.01).ToString()};
                var bodyStr = JsonConvert.SerializeObject(body);

                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
                {
                    var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                    return contentStr;
                }
            }
            else
            {
                var body = new {instrument_id = instrument_id, side = type, size = size, price = (float.Parse(alt) * 0.99).ToString()};
                var bodyStr = JsonConvert.SerializeObject(body);
                using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
                {
                    var contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                    return contentStr;
                }
            }
        }

        public static async Task<bool> posExist(string exchange, string inst, string api, string secret, string pass, FunctionClass fc, string WhoCalledMe)
        {
            Logger.Info($"calling okex posExist with these parameters: exchange {exchange} inst {inst}");

            var client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
            bool exist = true;
            if (inst == "Futures")
            {
                string getPos = await fc.GETvalidated(client, $"{BASEURL}api/futures/v3/{inst}/position", WhoCalledMe, api);
                float posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("long_qty").ToString());
                if (posSize == 0)
                {
                    posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("short_qty").ToString());
                }

                if (posSize == 0)
                {
                    exist = false;
                }
            }

            return exist;
        }

        public static async Task<string> order_algoFutures(FunctionClass fc, string exchange, string instrument_id, string type, string order_type, string size,
            string sweep_range, string sweep_ratio, string single_limit, string price_limit, string time_interval, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            //1:open long 2:open short 3:close long 4:close short
            Logger.Info(
                $"calling okex order_algoFutures with these parameters: exchange {exchange} instrument_id {instrument_id} type {type} order_type {order_type} size {size} sweep_range {sweep_range} sweep_ratio {sweep_ratio} price_limit {price_limit} time_interval {time_interval}");

            if (order_type == "2")
            {
                //size = (float.Parse(size) - 1).ToString();
            }

            float targetSize = 0;
            float posSize = 0;
            var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null));
            string getPos = "";
            if (exchange == "Futures")
            {
                getPos = await fc.GETvalidated(client, $"{BASEURL}api/futures/v3/{instrument_id}/position", WhoCalledMe, api);
            }

            if (exchange == "Swap")
            {
                getPos = await fc.GETvalidated(client, $"{BASEURL}api/swap/v3/{instrument_id}/position", WhoCalledMe, api);
            }

            if (exchange == "Futures")
            {
                posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("long_qty").ToString());
            }

            if (exchange == "Swap")
            {
                posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("position").ToString());
            }

            if (posSize == 0 && exchange == "Futures")
            {
                posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("short_qty").ToString());
            }

            if (order_type == "2")
            {
                string Str = await order_marketFutures(fc, exchange, instrument_id, type, "", size, api, secret, pass_phrase, WhoCalledMe);
                return Str;
            }

            if (order_type == "4")
            {
                try
                {
                    //MainTradingControl mainForm = _mainForm;
                    //mainForm.AddTwapCommandWatch(fc, clientName, "Futures", instrument_id, type, size, posSize.ToString(), time_interval, api, secret, pass_phrase, WhoCalledMe);
                }
                catch (Exception e)
                {
                    fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "order_algoFutures," + e.ToString());
                }
            }

            if (type == "3" || type == "4")
            {
                targetSize = posSize - float.Parse(size);
            }
            else
            {
                targetSize = posSize + float.Parse(size);
            }

            string contentStr = "";
            string url = "";
            if (exchange == "Futures")
            {
                url = $"{BASEURL}{FUTURES_SEGMENT}/order_algo";
            }

            if (exchange == "Swap")
            {
                url = $"{BASEURL}{SWAP_SEGMENT}/order_algo";
            }

            Object body = new Object();
            price_limit = Decimal.Parse((price_limit).ToString(), NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint).ToString();

            if (order_type == "4")
            {
                body = new
                {
                    instrument_id = instrument_id, type = type, order_type = order_type, size = size, sweep_range = sweep_range, sweep_ratio = sweep_ratio,
                    single_limit = single_limit, price_limit = price_limit, time_interval = time_interval
                };
            }

            if (order_type == "1")
            {
                size = posSize.ToString(); // for stoploss                
                body = new
                {
                    instrument_id = instrument_id, type = type, order_type = order_type, size = size, trigger_price = price_limit, algo_price = sweep_range
                };
            }

            var bodyStr = JsonConvert.SerializeObject(body);
            using (client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                if (order_type == "4")
                {
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TWAP order requested - , " + contentStr);
                }

                if (order_type == "1")
                {
                    fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Stop loss order requested - , " + contentStr);
                }

                return contentStr;
            }
            //int i = 0;
            //bool end = false;
            //float prevPosSize = 0;
            //int timer = 0;
            //float tenPer = 1;
            //client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null));
            //while (true) // amit yali to change
            //{
            //    Thread.Sleep(1000);
            //    i++;
            //    if (i % 10 == 0)
            //    { 

            //    }

            //    while (Math.Abs(posSize - startPosSize) / float.Parse(size) >= (tenPer) / 100)
            //    {
            //        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", $"{tenPer}% of TWAP done" );
            //        tenPer += 1;
            //    }
            //    getPos = await fc.GETvalidated(client, $"{BASEURL}api/futures/v3/{instrument_id}/position");                

            //    posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("long_qty").ToString());
            //    if (posSize == 0)
            //    {
            //        posSize = float.Parse(JToken.Parse(getPos).SelectToken("holding").First.SelectToken("short_qty").ToString());
            //    }
            //    if (posSize != prevPosSize)
            //    {
            //        timer = 1;
            //    }

            //    timer++;

            //    if (timer > (float.Parse(time_interval) * 2) / 1.5) // 1.3 seconds on avarege for this while, waiting for 2 time intervals 
            //    {
            //        end = true;
            //    }
            //    prevPosSize = posSize;
            //    if (end)
            //    {
            //        string closingSize = "";
            //        Thread.Sleep(500 * Convert.ToInt32(float.Parse(time_interval)));
            //        if (startPosSize > posSize)
            //        {
            //            closingSize = Math.Abs(startPosSize - posSize - float.Parse(size) - 1).ToString();
            //        }
            //        else
            //        {
            //            closingSize = Math.Abs(startPosSize - posSize + float.Parse(size) - 1).ToString();
            //        }
            //        //string contentStrB = await order_marketFutures(instrument_id, type,"1", closingSize, api, secret, pass_phrase);
            //        //fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TWAP limit order to finish, " + contentStrB);
            //        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "Two time intervals passed moving on, " + contentStr);

            //        return contentStr;
            //    }
            //}
        }

        public static async Task<string> order_algoMargin(FunctionClass fc, string instrument_id, string type, string order_type, string size,
            string sweep_range, string sweep_ratio, string single_limit, string price_limit, string time_interval, string api, string secret,
            string pass_phrase, string WhoCalledMe)
        {
            Logger.Info(
                $"calling okex order_algoMargin with these parameters: instrument_id {instrument_id} type {type} order_type {order_type} size {size} sweep_range {sweep_range} sweep_ratio {sweep_ratio} price_limit {price_limit} time_interval {time_interval}");

            string contentStr = "";
            if (order_type == "2")
            {
                contentStr = await order_marketMargin(fc, instrument_id, type, size, api, secret, pass_phrase, WhoCalledMe);
                return contentStr;
            }

            //float startPosSize = 0;
            float targetSize = 0;
            //float posSize = 0;
            if (type == "1" || type == "4")
            {
                type = "buy";
            }
            else
            {
                type = "sell";
            }

            string borrowed = (await get_borrowed(fc, instrument_id, api, secret, pass_phrase, WhoCalledMe));
            float posSize = 0;
            posSize = float.Parse(borrowed.Split(',')[3]);

            if (order_type == "4")
            {
                //MainTradingControl mainForm = _mainForm;
                //mainForm.AddTwapCommandWatch(fc, clientName, "Margin", instrument_id, type, size, posSize.ToString(), time_interval, api, secret, pass_phrase, WhoCalledMe);
            }

            if (targetSize < 0)
            {
                targetSize *= -1;
            }

            var url = $"{BASEURL}api/spot/v3/order_algo";
            Object body = new Object();
            price_limit = Decimal.Parse((price_limit).ToString(), NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint).ToString();

            if (order_type == "4")
            {
                body = new
                {
                    instrument_id = instrument_id, side = type, order_type = order_type, size = size, mode = 2, sweep_range = sweep_range,
                    sweep_ratio = sweep_ratio, single_limit = single_limit, limit_price = price_limit, time_interval = time_interval
                };
            }

            if (order_type == "1")
            {
                size = borrowed.Split(',')[2]; // for stoploss
                body = new
                {
                    instrument_id = instrument_id, side = type, order_type = order_type, size = size, mode = 2, trigger_price = price_limit,
                    algo_price = sweep_range
                };
            }

            var bodyStr = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr)))
            {
                if (size == "0")
                {
                }
                else
                {
                    contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
                }

                return contentStr;
            }
            //bool end = false;
            //int i = 0;
            //float prevPosSize = 0;
            //int timer = 0;
            //float tenPer = 1;
            //while (true) // amit yali to change
            //  {//100. if the size that needs to sell/buy < 5 => (only in margin) buy or sell in limit else {do in twap in (size -1) if(twap execute > 99%) then twap done then the rest at limit } 
            //    Thread.Sleep(1000);
            //    i++;
            //    if (i % 40 == 0)
            //    { // get max time interval
            //      //order canceled probably
            //    }
            //    while (Math.Abs(posSize - startPosSize) / float.Parse(size) >= (tenPer) / 100)
            //    {
            //        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", $"{tenPer }% of TWAP done");
            //        tenPer += 1;
            //    }
            //    borrowed = await get_borrowed(fc, instrument_id, api, secret, pass_phrase);
            //    posSize = float.Parse(borrowed.Split(',')[3]);

            //    if (posSize < 0)
            //    {
            //        posSize *= -1;
            //    }

            //    if (posSize != prevPosSize)
            //    {
            //        timer = 1;
            //    }

            //    timer++;

            //    if(timer> (float.Parse(time_interval)*2)/1.5) // 1.3 seconds on avarege for this while, waiting for 2 time intervals 
            //    {
            //        end = true;
            //    }
            //    prevPosSize = posSize;
            //    if (end)
            //    {
            //        string closingSize = "";
            //        Thread.Sleep(500 * Convert.ToInt32(float.Parse(time_interval)));
            //        if (startPosSize > posSize)
            //        {
            //            closingSize = Math.Abs(startPosSize - posSize - float.Parse(size) - 1).ToString();
            //        }
            //        else
            //        {
            //            closingSize = Math.Abs(startPosSize - posSize + float.Parse(size) - 1).ToString();
            //        }
            //        //string contentStrB = await order_marketSpot(instrument_id, type, closingSize, api, secret, pass_phrase);
            //       // fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TWAP limit order to finish, " + contentStrB);

            //        return contentStr;
            //    }
        }

        public static async Task<string> order_algoSpot(FunctionClass fc, string instrument_id, string type, string order_type, string size, string sweep_range,
            string sweep_ratio, string single_limit, string price_limit, string time_interval, string api, string secret, string pass_phrase,
            string WhoCalledMe)
        {
            Logger.Info(
                $"calling okex order_algoSpot with these parameters: instrument_id {instrument_id} type {type} order_type {order_type} size {size} sweep_range {sweep_range} sweep_ratio {sweep_ratio} price_limit {price_limit} time_interval {time_interval}");

            string contentStr = "";
            var client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null));
            if (order_type == "2")
            {
                //contentStr = await order_marketSpot(instrument_id, type, size, api, secret, pass_phrase);
                // return contentStr;
            }

            if (order_type == "4")
            {
                // size = (float.Parse(size) - 1).ToString();
            }

            //float startPosSize = 0;
            //float targetSize = 0;
            float posSize = 0;
            if (type == "1" || type == "4")
            {
                type = "buy";
            }
            else
            {
                type = "sell";
            }

            string spotPos = "";
            spotPos = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/accounts/{instrument_id.Split('-')[0]}", WhoCalledMe, api);
            if (type == "buy")
            {
                // buy - second sell - first
                posSize = float.Parse(JToken.Parse(spotPos).SelectToken("available").ToString());
            }
            else
            {
                posSize = float.Parse(JToken.Parse(spotPos).SelectToken("available").ToString());
            }

            if (order_type == "4")
            {
                //MainTradingControl mainForm = _mainForm;
                //mainForm.AddTwapCommandWatch(fc, clientName, "Spot", instrument_id, type, size, posSize.ToString(), time_interval, api, secret, pass_phrase, WhoCalledMe);
            }

            var url = $"{BASEURL}api/spot/v3/order_algo";
            Object body = new Object();
            price_limit = Decimal.Parse((price_limit).ToString(), NumberStyles.AllowExponent | NumberStyles.AllowDecimalPoint).ToString();
            if (order_type == "4")
            {
                body = new
                {
                    instrument_id = instrument_id, side = type, order_type = order_type, size = size, mode = 1, sweep_range = sweep_range,
                    sweep_ratio = sweep_ratio, single_limit = single_limit, limit_price = price_limit, time_interval = time_interval
                };
            }

            if (order_type == "1")
            {
                size = posSize.ToString(); // for stoploss
                body = new
                {
                    instrument_id = instrument_id, side = type, order_type = order_type, size = size, mode = 1, trigger_price = price_limit,
                    algo_price = sweep_range
                };
            }

            var bodyStr = JsonConvert.SerializeObject(body);
            client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, bodyStr));

            contentStr = await fc.POSTvalidated(new HttpInterceptor(api, secret, pass_phrase, bodyStr), bodyStr, url, fc, WhoCalledMe);
            return contentStr;

            //client = new HttpClient(new HttpInterceptor(api, secret, pass_phrase, null));
            //bool end = false;
            //int i = 0;
            //float prevPosSize = 0;
            //int timer = 0;
            //float tenPer = 1;
            //while (true) // amit yali to change
            //{//100. if the size that needs to sell/buy < 5 => buy or sell in limit else {do in twap in (size -1) if(twap execute > 99%) then twap done then the rest at limit } 
            //    Thread.Sleep(1000);
            //    i++;
            //    if (i > 40)
            //    { // get max time interval
            //      //order canceled probably
            //    }
            //    while (Math.Abs(posSize - startPosSize) / float.Parse(size) >= (tenPer) / 100)
            //    {
            //        fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", $"{tenPer }% of TWAP done");
            //        tenPer += 1;
            //    }
            //    spotPos = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/accounts/{instrument_id.Split('-')[0]}");
            //    posSize = float.Parse(JToken.Parse(spotPos).SelectToken("available").ToString());

            //    if (posSize < 0)
            //    {
            //        posSize *= -1;
            //    }

            //    if (posSize != prevPosSize)
            //    {
            //        timer = 1;
            //    }


            //    timer++;

            //    if (timer > (float.Parse(time_interval) * 2) / 1.3) // 1.3 seconds on avarege for this while, waiting for 2 time intervals 
            //    {
            //        end = true;
            //    }
            //    prevPosSize = posSize;

            //    if (end)
            //    {
            //        Thread.Sleep(500 * Convert.ToInt32(float.Parse(time_interval)));

            //        string closingSize = "";
            //        if (startPosSize > posSize)
            //        {
            //             closingSize = Math.Abs(startPosSize - posSize - float.Parse(size) - 1).ToString();
            //        }
            //        else
            //        {
            //             closingSize = Math.Abs(startPosSize - posSize + float.Parse(size) - 1).ToString();
            //        }
            //        //string contentStrB = await order_marketSpot(instrument_id, type, closingSize, api, secret, pass_phrase);
            //       // fc.WriteMessageToFile(WhoCalledMe, "General_All_Log.csv", "TWAP limit order to finish, " + contentStrB);

            //        return contentStr;
            //    }
            //}
        }

        public static async Task<Wallet> Get_Wallet_Spot(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            HttpClient client = new HttpClient();
            client = new HttpClient(new HttpInterceptor(api, secret, pass, null));

            var result = "";
            JToken json;
            JToken currency;
            Wallet Main_wallet = new Wallet(fc);
            Wallet Funding_wallet = new Wallet(fc);
            ;
            Wallet[] wallets;
            string eq = "";
            string inst = "";
            string type = "";
            float total_eq_btc = 0;
            float total_eq_usd = 0;
            string line = "";
            result = await fc.GETvalidated(client, $"{BASEURL}{SPOT_WALLET}", WhoCalledMe, api);
            json = JToken.Parse(result);
            wallets = new Wallet[json.Count()];
            currency = json.First;
            Wallet Spot_wallet = new Wallet(fc);
            Spot_wallet._wallets = new Wallet[json.Count()];
            wallets = Spot_wallet._wallets;
            eq = "";
            inst = "";
            type = "";
            for (int i = 0; i < json.Count(); i++)
            {
                if (i > 0)
                {
                    currency = currency.Next;
                }

                type = "spot";
                inst = currency.SelectToken("currency").ToString();
                eq = currency.SelectToken("balance").ToString();
                Wallet wallet = new Wallet(fc, type, inst, eq, api);
                wallets[i] = wallet;
            }

            for (int i = 0; i < wallets.Length; i++)
            {
                line = await wallets[i].get_eq_0Async(WhoCalledMe);
                total_eq_btc += float.Parse(line.Split(',')[0]);
                total_eq_usd += float.Parse(line.Split(',')[1]);
                //Console.WriteLine(await wallets[i].get_infoAsync());
                //File.AppendAllText(fc.GetPath() + "/output.csv", await wallets[i].get_infoAsync() + System.Environment.NewLine); // creating a new file
            }

            Spot_wallet._USDeq = total_eq_usd;
            Spot_wallet._BTCeq = total_eq_btc;
            total_eq_btc = 0;
            total_eq_usd = 0;
            Spot_wallet._wallets = wallets;
            return Spot_wallet;
        }

        public static async Task<Wallet> Get_Wallet_Futures(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            HttpClient client = new HttpClient();
            client = new HttpClient(new HttpInterceptor(api, secret, pass, null));

            var result = "";
            JToken json;
            JToken currency;
            Wallet[] wallets;
            string eq = "";
            string inst = "";
            string type = "";
            float total_eq_btc = 0;
            float total_eq_usd = 0;
            string line = "";
            var run = true;
            while (run) //beacuse limited to 1 request per second
            {
                Thread.Sleep(3000);

                try
                {
                    result = await fc.GETvalidated(client, $"{BASEURL}{FUTURES_SEGMENT}/accounts", WhoCalledMe, api);
                    run = false;
                }
                catch
                {
                }
            }

            json = JToken.Parse(result);
            json = json.First.First;
            wallets = new Wallet[json.Count()];
            Wallet Futures_wallet = new Wallet(fc);
            Futures_wallet._wallets = new Wallet[json.Count()];
            wallets = Futures_wallet._wallets;
            if (json.First == null)
            {
                Futures_wallet._BTCeq = 0;
                Futures_wallet._USDeq = 0;
            }
            else
            {
                currency = json.First.First;
                //Console.WriteLine(currency.SelectToken("currency"));

                eq = "";
                inst = "";
                type = "";
                for (int i = 0; i < json.Count(); i++)
                {
                    if (i > 0)
                    {
                        currency = currency.Parent.Next.First;
                    }

                    type = "futures";
                    inst = currency.SelectToken("currency").ToString();
                    eq = currency.SelectToken("equity").ToString();
                    Wallet wallet = new Wallet(fc, type, inst, eq, api);
                    wallets[i] = wallet;
                }

                for (int i = 0; i < wallets.Length; i++)
                {
                    line = await wallets[i].get_eq_0Async(WhoCalledMe);
                    wallets[i]._USDeq = float.Parse(line.Split(',')[1]);
                    total_eq_btc += float.Parse(line.Split(',')[0]);
                    total_eq_usd += float.Parse(line.Split(',')[1]);
                    //Console.WriteLine(await wallets[i].get_infoAsync());
                    //File.AppendAllText(fc.GetPath() + "/output.csv", await wallets[i].get_infoAsync() + System.Environment.NewLine); // creating a new file
                }

                Futures_wallet._USDeq = total_eq_usd;
                Futures_wallet._BTCeq = total_eq_btc;
                total_eq_btc = 0;
                total_eq_usd = 0;
                Futures_wallet._wallets = wallets;
            }

            return Futures_wallet;
        }

        public static async Task<Wallet> Get_Wallet_Margin(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            HttpClient client = new HttpClient();
            client = new HttpClient(new HttpInterceptor(api, secret, pass, null));

            //var result = "";
            JToken json;
            //JToken currency;
            Wallet Main_wallet = new Wallet(fc);
            Wallet Funding_wallet = new Wallet(fc);
            ;
            Wallet[] wallets;
            string eq = "";
            string inst = "";
            string type = "";
            float total_eq_btc = 0;
            float total_eq_usd = 0;
            //string line = "";
            var lines = await get_borrowed7(fc, api, secret, pass, WhoCalledMe);
            wallets = new Wallet[lines.Length];
            Wallet Margin_wallet = new Wallet(fc);
            Margin_wallet._wallets = new Wallet[lines.Length];
            wallets = Margin_wallet._wallets;
            Margin_wallet._BTCeq = 0;
            Margin_wallet._USDeq = 0;

            eq = "";
            string borrowed = "";
            inst = "";
            type = "";
            string convert = "";
            for (int i = 0; i < lines.Length; i++)
            {
                type = "margin";
                inst = lines[i].Split(',')[0];
                eq = lines[i].Split(',')[2]; // special equity
                borrowed = lines[i].Split(',')[3];
                convert = lines[i].Split(',')[4];
                Wallet wallet = new Wallet(fc, type, inst, eq, api);
                //string eqToOther = await wallet.get_eq_marginAsync(); // transfering eq to usd and btc
                if (convert == "USDT")
                {
                    wallet._type = "toUSDT";
                    wallet._borrowedUSDT = lines[i].Split(',')[6];
                    wallet._USDeq = float.Parse(lines[i].Split(',')[5]); // + float.Parse(eqToOther.Split(',')[1]);
                }
                else
                {
                    wallet._type = "toBTC";
                    wallet._borrowedBTC = lines[i].Split(',')[6];
                    wallet._BTCeq = float.Parse(lines[i].Split(',')[5]); // + float.Parse(eqToOther.Split(',')[0]);
                }

                wallet._borrowedEq = borrowed;
                wallets[i] = wallet;
            }

            HttpClient Get_client = new HttpClient();
            string BTC = await fc.GETvalidated(client, $"{BASEURL}api/spot/v3/instruments/BTC-USDT/ticker", WhoCalledMe, api);
            json = JObject.Parse(BTC);
            float BTCValue = float.Parse(json.SelectToken("last").ToString());
            for (int i = 0; i < wallets.Length; i++)
            {
                if (wallets[i]._type == "toUSDT")
                {
                    total_eq_btc += wallets[i]._USDeq / BTCValue;
                    total_eq_usd += wallets[i]._USDeq;
                }
                else
                {
                    total_eq_btc += wallets[i]._BTCeq;
                    total_eq_usd += wallets[i]._BTCeq * BTCValue;
                }

                //Console.WriteLine(await wallets[i].get_infoAsync());
                //File.AppendAllText(fc.GetPath() + "/output.csv", await wallets[i].get_infoAsync() + System.Environment.NewLine); // creating a new file
            }

            Margin_wallet._USDeq = total_eq_usd;
            Margin_wallet._BTCeq = total_eq_btc;
            total_eq_btc = 0;
            total_eq_usd = 0;
            Margin_wallet._wallets = wallets;
            Margin_wallet._name = "Margin";
            return Margin_wallet;
        }

        public static async Task<Wallet> Get_Wallet_Funding(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            HttpClient client = new HttpClient();
            var result = "";
            JToken json;
            JToken currency;
            Wallet Main_wallet = new Wallet(fc);
            Wallet Funding_wallet = new Wallet(fc);
            ;
            Wallet[] wallets;
            string eq = "";
            string inst = "";
            string type = "";
            float total_eq_btc = 0;
            float total_eq_usd = 0;
            string line = "";

            try
            {
                client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
                result = await fc.GETvalidated(client, $"{BASEURL}{WALLET_SEGMENT}", WhoCalledMe, api); //funding            
                json = JToken.Parse(result);
                currency = json.First;
                Main_wallet = new Wallet(fc);
                Main_wallet._wallets = new Wallet[4];
                Funding_wallet = new Wallet(fc);
                Funding_wallet._wallets = new Wallet[json.Count()];
                wallets = Funding_wallet._wallets;
                for (int i = 0; i < json.Count(); i++)
                {
                    if (i > 0)
                    {
                        currency = currency.Next;
                    }

                    type = "funding";
                    inst = currency.SelectToken("currency").ToString();
                    eq = currency.SelectToken("balance").ToString();
                    Wallet wallet = new Wallet(fc, type, inst, eq, api);
                    wallets[i] = wallet;
                }

                for (int i = 0; i < wallets.Length; i++)
                {
                    try
                    {
                        line = await wallets[i].get_eq_0Async(WhoCalledMe);
                    }
                    catch (Exception e)
                    {
                        fc.WriteMessageToFile(WhoCalledMe, "Exception, Get_Wallet - get_eq_0Async,", e.ToString());
                    }

                    total_eq_btc += float.Parse(line.Split(',')[0]);
                    total_eq_usd += float.Parse(line.Split(',')[1]);
                }

                Funding_wallet._USDeq = total_eq_usd;
                Funding_wallet._BTCeq = total_eq_btc;
                total_eq_btc = 0;
                total_eq_usd = 0;
                Funding_wallet._wallets = wallets;
            }
            catch (Exception e)
            {
                fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "Get_Wallet - //funding," + e.ToString());
            }

            return Funding_wallet;
        }

        public static async Task<Wallet> Get_Wallet(FunctionClass fc, string api, string secret, string pass, string WhoCalledMe)
        {
            Wallet Main_wallet = new Wallet(fc);
            Main_wallet._wallets = new Wallet[4];
            //end
            Main_wallet._wallets[0] = await Get_Wallet_Funding(fc, api, secret, pass, WhoCalledMe);
            Main_wallet._wallets[1] = await Get_Wallet_Spot(fc, api, secret, pass, WhoCalledMe);
            Main_wallet._wallets[2] = await Get_Wallet_Futures(fc, api, secret, pass, WhoCalledMe);
            Main_wallet._wallets[3] = await Get_Wallet_Margin(fc, api, secret, pass, WhoCalledMe);

            foreach (Wallet wall in Main_wallet._wallets)
            {
                if (wall._name != "Margin")
                {
                    Main_wallet._USDeq += wall._USDeq;
                    Main_wallet._BTCeq += wall._BTCeq;
                }
            }

            HttpClient Get_client = new HttpClient();
            string BTC = await fc.GETvalidated(Get_client, $"{BASEURL}api/spot/v3/instruments/BTC-USDT/ticker", WhoCalledMe);
            JObject json = JObject.Parse(BTC);
            float BTCValue = float.Parse(json.SelectToken("last").ToString());
            string marginTotal = await get_margin_total(fc, api, secret, pass, WhoCalledMe);
            Main_wallet._BTCeq += float.Parse(marginTotal);
            Main_wallet._USDeq += BTCValue * float.Parse(marginTotal);

            return Main_wallet;
            //File.AppendAllText(fc.GetPath() + "/total.csv", total_eq_usd + "," + total_eq_btc + "," + api + System.Environment.NewLine); // put account name her
        }
    }
}