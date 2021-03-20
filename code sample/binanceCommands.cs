using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Common
{
    public class binanceCommands
    {
        public static async Task<string> getSpotAvail(string inst, string api, string secret)
        {
            string ret = await getSpotData(api, secret);
            JToken token = JToken.Parse(ret).SelectToken("balances").First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("asset").ToString() == inst)
                {
                    ret = token.SelectToken("free").ToString();
                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> test()
        {
            Logger.Info("Calling Binance command test");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "SPOT");

            string ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/accountSnapshot", "s", "", "", false,
                parameters);
            return ha;
        }

        public static async Task<string> orderMarketFutures(string symbol, string side, string quantity, string api,
            string secret)
        {
            Logger.Info($"Calling Binance command orderMarketFutures with symbol: {symbol}, side: {side}, quantity: {quantity}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "MARKET");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);

            string ha = await binanceHttpClass.CallAsync("POST", "dapi/v1/order", "Futures", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<string> orderStopFutures(string symbol, string side, string quantity, string price,
            string stopPrice, string api, string secret)
        {
            Logger.Info(
                $"Calling Binance command orderStopFutures with symbol: {symbol}, side: {side}, quantity: {quantity}, price: {price}, stopPrice: {stopPrice}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "STOP");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);
            parameters.Set("price", price);
            parameters.Set("stopPrice", stopPrice);

            string ha = await binanceHttpClass.CallAsync("POST", "dapi/v1/order", "Futures", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<string> changeFuturesLeverage(string symbol, string leverage, string api,
            string secret)
        {
            var parameters = HttpUtility.ParseQueryString(string.Empty);

            try
            {
                Logger.Info(
                    $"Calling Binance command changeFuturesLeverage with symbol: {symbol}, leverage: {leverage}");
                parameters = HttpUtility.ParseQueryString(string.Empty);
                parameters.Set("marginType", "ISOLATED");
                parameters.Set("symbol", symbol);

                string marginRet = await binanceHttpClass.CallAsync("POST", "dapi/v1/marginType", "Futures", api,
                    secret,
                    true, parameters);
            }
            catch (BinanceErrorResponseException e)
            {
                // TODO: deal with this exception - make sure we don't make the call above unless we need to
                if (!e.Message.Contains("No need to change margin type."))
                {
                    throw;
                }
            }

            parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);
            parameters.Set("leverage", leverage);

            string ha = await binanceHttpClass.CallAsync("POST", "dapi/v1/leverage", "Futures", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<string> getAllFuturesOrders(string symbol, string api, string secret)
        {
            Logger.Info($"Calling Binance command getAllFuturesOrders with symbol: {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/openOrders", "Futures", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<List<JToken>> getSpotCandle(string symbol)
        {
            Logger.Info($"Calling getSpotCandle with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "api/v3/klines", "a", "", "", false, parameters);
            return JToken.Parse(ha).Reverse().ToList();
        }

        public static async Task<List<JToken>> getFuturesCandle(string symbol)
        {
            Logger.Info($"Calling getFuturesCandle with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/klines", "Del", "", "", false, parameters);
            return JToken.Parse(ha).Reverse().ToList();
        }

        public static async Task<List<Candle>> GetCandles(string symbol)
        {
            Logger.Info($"Calling GetCandles with symbol :{symbol}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            var endpoint = "api/v3/klines";
            var exchange = "a";
            if (symbol.Split('-').Length > 2)
            {
                endpoint = "dapi/v1/klines";
                exchange = "Del";
            }

            var ha = await binanceHttpClass.CallAsync("GET", endpoint, exchange, "", "", false, parameters);
            var list = JToken.Parse(ha).Reverse().ToList();

            var res = new List<Candle>();
            foreach (var jCandle in list)
            {
                var candle = jCandle.ToList();
                var unixDateTime = (long) candle[0];
                res.Add(new Candle
                {
                    Date = UnixTimeStampToDateTime(unixDateTime),
                    Open = (decimal) candle[1],
                    High = (decimal) candle[2],
                    Low = (decimal) candle[3],
                    Close = (decimal) candle[4],
                });
            }

            return res;
        }

        public static async Task<List<JToken>> getSwap24HCandle(string symbol)
        {
            Logger.Info($"Calling getSwap24HCandle with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "fapi/v1/klines", "Swap", "", "", false, parameters);
            return JToken.Parse(ha).Reverse().ToList();
        }

        public static async Task<string> getSwapFR(string symbol)
        {
            Logger.Info($"Calling getSwapFR with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "fapi/v1/fundingRate", "Swap", "", "", false,
                parameters);
            return ha;
        }

        // TODO: Remove?
        public static async Task<string> getFuturesLimitOrder(string symbol, string api, string secret)
        {
            string ret = await getAllFuturesOrders(symbol, api, secret);
            JToken token = JToken.Parse(ret).First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("origType").ToString() == "LIMIT")
                {
                    return token.ToString();
                }
                else
                {
                    token = token.Next;
                }
            }

            return "NOLIMIT";
        }

        public static async Task<string> getFuturesPosMargin(string inst, string api, string secret)
        {
            string ret = await getFuturesPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = (decimal.Parse(token.SelectToken("isolatedMargin").ToString()) -
                           decimal.Parse(token.SelectToken("unRealizedProfit").ToString())).ToString();
                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> getFuturesPosCost(string inst, string api, string secret)
        {
            string ret = await getFuturesPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("entryPrice").ToString();
                    if (token.SelectToken("positionAmt").ToString().Contains('-'))
                    {
                        ret = "-" + ret;
                    }

                    if (token.SelectToken("positionAmt").ToString() == "0")
                    {
                        ret = "NOPOS";
                    }

                    break;
                    if (token == null)
                    {
                        return "0";
                    }
                }

                token = token.Next;
            }

            return ret;
        }

        public static async Task<string> getFuturesPosLiq(string inst, string api, string secret)
        {
            string ret = await getFuturesPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("liquidationPrice").ToString();
                    if (token.SelectToken("positionAmt").ToString().Contains('-'))
                    {
                        ret = "-" + ret;
                    }

                    if (token.SelectToken("positionAmt").ToString() == "0")
                    {
                        ret = "NOPOS";
                    }

                    break;
                    if (token == null)
                    {
                        return "0";
                    }
                }

                token = token.Next;
            }

            return ret;
        }

        public static async Task<string> getFuturesPosQty(string inst, string api, string secret)
        {
            string ret = await getFuturesPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("positionAmt").ToString();
                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> GetPositionMarginHistory(string symbol, string api,
            string secret)
        {
            Logger.Info($"Calling getPositionMarginHistory for instrument: {symbol}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/positionMargin/history", "Futures", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<string> changePosMarginFutures(string symbol, string amount, string type, string api,
            string secret)
        {
            Logger.Info($"Calling changePosMarginFutures for instrument: {symbol} with amount: {amount}, type: {type}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);
            parameters.Set("amount", amount);
            parameters.Set("type", type);
            try
            {
                string ha = await binanceHttpClass.CallAsync("POST", "dapi/v1/positionMargin", "Futures", api, secret, true,
                    parameters);
                return ha;
            }
            catch (BinanceErrorResponseException e)
            {
                if (!e.Message.Contains("Internal error: 1"))
                {
                    throw;
                }

                // Binance returns a BadRequest if there are no open orders, for now we can ignore this exception
                Logger.Warn("changePosMarginFutures threw the 'Internal error: 1' error. Ignore it for now.");
            }

            return "";
        }

        public static async Task<string> ChangePosMarginFuturesTryLoweringAmount(string instrument, decimal amount,
            string type, string api, string secret)
        {
            var initialAmount = amount;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    return await changePosMarginFutures(instrument, DecimalsHelper.ToString(amount), type, api, secret);
                }
                catch (BinanceErrorResponseException e)
                {
                    if (!e.Message.Contains("Isolated balance insufficient"))
                    {
                        throw;
                    }

                    amount *= 0.85m;
                }
            }

            throw new Exception(
                $"ChangePosMarginSwapTryLoweringAmount did not succeed after 5 attempts. Initial amount was {initialAmount}, final amount was: {amount}");
        }

        public static async Task<string> getFuturesPositionData(string api, string secret)
        {
            Logger.Info($"Calling getFuturesPositionData");

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/positionRisk", "Futures", api, secret, true,
                null);
            return ha;
        }

        public static async Task<string> getFuturesPrice(string symbol)
        {
            Logger.Info($"Calling getFuturesPrice with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/ticker/price", "Futures", "", "", false,
                parameters);
            return JToken.Parse(ha).First.SelectToken("price").ToString();
        }

        public static async Task<string> getFuturesData(string api, string secret)
        {
            Logger.Info($"Calling getFuturesPrice");

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/account", "Futures", api, secret, true);
            return ha;
        }

        public static async Task<string> getFuturesBalance(string api, string secret)
        {
            Logger.Info($"Calling getFuturesBalance");

            string ha = await binanceHttpClass.CallAsync("GET", "dapi/v1/balance", "Futures", api, secret, true);
            return ha;
        }

        public static async Task<string> cancelOrdersFutures(string symbol, string api, string secret)
        {
            Logger.Info($"Calling cancelOrdersFutures with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            return await binanceHttpClass.CallAsync("DEL", "dapi/v1/allOpenOrders", "Futures", api, secret, true,
                parameters);
        }

        public static async Task<string> transferSpotFutures(string amount, string asset, string type, string api,
            string secret)
        {
            Logger.Info($"Calling transferSpotFutures with amount :{amount}, asset: {asset}, type: {type}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("asset", asset);
            parameters.Set("amount", amount);
            parameters.Set("type", type); //types - 3 = spot to Futures 4 = Futures to spot

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/futures/transfer", "Reg", api, secret, true,
                parameters);
        }
        //----------------------------------------------------------------------------------------------------------------------------

        public static async Task<string> orderMarketSwap(string symbol, string side, string quantity, string api,
            string secret)
        {
            Logger.Info($"Calling orderMarketSwap with symbol :{symbol}, side: {side}, quantity: {quantity}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "MARKET");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);

            return await binanceHttpClass.CallAsync("POST", "fapi/v1/order", "Swap", api, secret, true,
                parameters);
        }

        public static async Task<string> orderStopSwap(string symbol, string side, string quantity, string price,
            string stopPrice, string api, string secret)
        {
            Logger.Info($"Calling orderStopSwap with symbol :{symbol}, side: {side}, quantity: {quantity}, price: {price}, stopPrice: {stopPrice}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "STOP");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);
            parameters.Set("price", price);
            parameters.Set("stopPrice", stopPrice);

            return await binanceHttpClass.CallAsync("POST", "fapi/v1/order", "Swap", api, secret, true,
                parameters);
        }

        public static async Task<string> changeSwapLeverage(string symbol, string leverage, string api, string secret)
        {
            Logger.Info($"Calling changeSwapLeverage with symbol :{symbol}, leverage: {leverage}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("marginType", "ISOLATED");
            parameters.Set("symbol", symbol);

            await binanceHttpClass.CallAsync("POST", "fapi/v1/marginType", "Swap", api, secret, true, parameters);

            parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);
            parameters.Set("leverage", leverage);

            return await binanceHttpClass.CallAsync("POST", "fapi/v1/leverage", "Swap", api, secret, true,
                parameters);
        }

        public static async Task<string> swapLoan(string coin, string collateralCoin, string collateralAmount, string api, string secret)
        {
            Logger.Info($"Calling swapLoan with coin :{coin}, collateralCoin: {collateralCoin}, collateralAmount: {collateralAmount}");

            if (collateralAmount == "0")
            {
                Logger.Warn("Attempting to send a swapLoan request with '0' collateralAmount, aborting request");
                return "";
            }

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("coin", coin);
            parameters.Set("collateralCoin", collateralCoin);
            parameters.Set("collateralAmount", collateralAmount);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/futures/loan/borrow", "s", api, secret, true,
                parameters);
        }

        public static async Task<string> swapRepay(string coin, string collateralCoin, string amount, string api, string secret)
        {
            Logger.Info($"Calling swapRepay with coin :{coin}, collateralCoin: {collateralCoin}, amount: {amount}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("coin", coin);
            parameters.Set("collateralCoin", collateralCoin);
            parameters.Set("amount", amount);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/futures/loan/repay", "s", api, secret, true,
                parameters);
        }

        public static async Task<string> adjustColSwap(string direction, string collateralCoin, string amount,
            string api, string secret)
        {
            if (direction == "1") // translating okex adjust margin?
            {
                direction = "ADDITIONAL";
            }

            if (direction == "2")
            {
                direction = "REDUCED";
            }

            Logger.Info($"Calling adjustColSwap with direction :{direction}, collateralCoin: {collateralCoin}, amount: {amount}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("direction", direction);
            parameters.Set("collateralCoin", collateralCoin);
            parameters.Set("amount", amount);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/futures/loan/adjustCollateral", "s", api,
                secret, true, parameters);
        }

        public static async Task<string> orderMarketSpot(string symbol, string side, string quantity, string api,
            string secret)
        {
            Logger.Info($"Calling orderMarketSpot with symbol :{symbol}, side: {side}, quantity: {quantity}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "MARKET");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);

            return await binanceHttpClass.CallAsync("POST", "api/v3/order", "s", api, secret, true, parameters);
        }

        public static async Task<string> getAllSwapOrders(string symbol, string api, string secret)
        {
            Logger.Info($"Calling orderMarketSpot with symbol :{symbol}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            return await binanceHttpClass.CallAsync("GET", "fapi/v1/openOrders", "Swap", api, secret, true,
                parameters);
        }

        public static async Task<string> getSwapLimitOrder(string symbol, string api, string secret)
        {
            string ret = await getAllSwapOrders(symbol, api, secret);
            JToken token = JToken.Parse(ret).First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("origType").ToString() == "LIMIT")
                {
                    return token.ToString();
                }
                else
                {
                    token = token.Next;
                }
            }

            return "NOLIMIT";
        }

        public static async Task<string> orderStopSpot(string symbol, string side, string quantity, string price,
            string stopPrice, string api, string secret)
        {
            if (side == "2" || side == "3")
            {
                side = "SELL";
            }

            if (side == "1" || side == "4")
            {
                side = "BUY";
            }

            Logger.Info($"Calling orderStopSpot with symbol :{symbol}, side: {side}, quantity: {quantity}, price: {price}, stopPrice: {stopPrice}");

            var quantityRounding = 5;
            var priceRounding = 5;
            price = Math.Round(float.Parse(price), priceRounding + 2).ToString("F8");
            stopPrice = Math.Round(float.Parse(stopPrice), priceRounding + 2).ToString("F8");
            quantity = Math.Floor(float.Parse(quantity)).ToString("F" + (3 + quantityRounding));

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "STOP_LOSS_LIMIT");
            parameters.Set("timeInForce", "GTC");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);
            parameters.Set("price", price);
            parameters.Set("stopPrice", stopPrice);

            while (quantityRounding > 0 && priceRounding > 0)
            {
                try
                {
                    // will return once successful
                    return await binanceHttpClass.CallAsync("POST", "api/v3/order", "s", api, secret, true, parameters);
                }
                catch (BinanceErrorResponseException e)
                {
                    if (!e.Message.Contains("LOT") && !e.Message.Contains("PRICE"))
                    {
                        throw;
                    }

                    if (e.Message.Contains("LOT"))
                    {
                        Logger.Warn("in orderStopSpot got error that quantity is too big, trying to round to a higher decimal");

                        quantityRounding -= 1;
                        quantity = Math.Floor(float.Parse(quantity)).ToString("F" + (3 + quantityRounding));
                        parameters.Set("quantity", quantity);
                    }

                    if (e.Message.Contains("PRICE"))
                    {
                        Logger.Warn("in orderStopSpot got error that price is too high, trying to round to a higher decimal");

                        priceRounding -= 1;
                        price = Math.Round(float.Parse(price), 3 + 2).ToString("F" + (3 + priceRounding));
                        stopPrice = Math.Round(float.Parse(stopPrice), 3 + 2).ToString("F" + (3 + priceRounding));
                        parameters.Set("price", price);
                        parameters.Set("stopPrice", stopPrice);
                    }

                    if (quantityRounding == 0 || priceRounding == 0)
                    {
                        Logger.Error("in orderStopSpot Failed to round up enough after 5 iterations");
                        throw;
                    }
                }
            }

            // This return should be unreachable
            return "";
        }
        //public static async Task<string> Get_max(string subWallet, string currency, string inst, string api, string secret, string pass, FunctionClass fc, string WhoCalledMe)
        //{
        //    string ammount = "";
        //    HttpClient client = new HttpClient(new HttpInterceptor(api, secret, pass, null));
        //    try
        //    {
        //        if (subWallet == "Margin")
        //        {
        //            string result = await get_borrowed(fc, inst, api, secret, pass, WhoCalledMe);


        //            if (result.Split(',')[4] == currency)
        //            {
        //                ammount = result.Split(',')[10];
        //            }
        //            else
        //            {
        //                ammount = result.Split(',')[9];
        //            }
        //            ammount = (double.Parse(ammount) - 0.01).ToString();

        //        }
        //        if (subWallet == "Swap")
        //        {
        //            string result = await fc.GETvalidated(client, $"{BASEURL}{Swap_SEGMENT}/accounts", WhoCalledMe);
        //            JObject json = JObject.Parse(result);
        //            JToken token = json.First.First;
        //            if (inst == "N/A")
        //            {
        //                if (token != null)
        //                {
        //                    token = token.SelectToken(currency.ToLower());
        //                    token = token.SelectToken("total_avail_balance");
        //                }
        //            }
        //            else
        //            {
        //                token = token.SelectToken((inst.Split('-')[0] + "-" + inst.Split('-')[1]).ToLower());
        //                if (token == null)
        //                {
        //                    return "nothing in future instrument";
        //                }
        //                token = token.SelectToken("total_avail_balance");
        //            }
        //            if (token == null)
        //            {
        //                ammount = "0";
        //            }
        //            else
        //            {
        //                ammount = token.ToString();
        //            }
        //        }
        //        if (subWallet == "Spot")
        //        {
        //            string result = await fc.GETvalidated(client, $"{BASEURL}{SPOT_WALLET}/{currency}", WhoCalledMe);
        //            JToken json = JToken.Parse(result);
        //            ammount = json.SelectToken("balance").ToString();
        //        }
        //        if (subWallet == "Funding")
        //        {
        //            string result = await fc.GETvalidated(client, $"{BASEURL}api/account/v3/wallet/{currency}", WhoCalledMe);
        //            JToken json = JToken.Parse(result);
        //            ammount = json.SelectToken("balance").ToString();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        fc.WriteMessageToFile(WhoCalledMe, "Exception Log.csv", "get_max," + e.ToString());
        //    }
        //    if (ammount == null) { ammount = "0"; }
        //    return ammount;
        //}

        public static async Task<string> getMax(string subWallet, string currency, string inst, string api,
            string secret)
        {
            string amount = "0";
            JToken token = null;
            string ret = "";
            if (subWallet == "Margin")
            {
                string result = await get_borrowed(inst, api, secret);
                if (result.Split(',')[1] == currency)
                {
                    amount = result.Split(',')[3];
                }
                else
                {
                    amount = result.Split(',')[6];
                }

                if (double.Parse(amount) > 0.5)
                {
                    amount = (double.Parse(amount) - 0.01).ToString();
                }
            }

            if (subWallet == "Swap")
            {
                ret = await getSwapData(api, secret);
                ret = findAssetInUnnamedToken(JToken.Parse(ret).SelectToken("assets"), currency);
                amount = JToken.Parse(ret).SelectToken("maxWithdrawAmount").ToString();
            }

            if (subWallet == "Futures")
            {
                ret = await getFuturesData(api, secret);
                ret = findAssetInUnnamedToken(JToken.Parse(ret).SelectToken("assets"), currency);
                amount = JToken.Parse(ret).SelectToken("maxWithdrawAmount").ToString();
            }

            if (subWallet == "Spot")
            {
                ret = await getSpotData(api, secret);
                ret = findAssetInUnnamedToken(JToken.Parse(ret).SelectToken("balances"), currency);
                amount = JToken.Parse(ret).SelectToken("free").ToString();
            }

            return amount;
        }

        public static async Task<string> get_borrowed(string inst, string api, string secret)
        {
            Logger.Debug($"Received string with length of {inst.Length}");
            string ret = "";
            string currency1Ret = "";
            string currency2Ret = "";
            try
            {
                string currency1 = inst.Split('-')[0];
                string currency2 = inst.Split('-')[1];
                string marginData = await getMarginData(api, secret);
                JToken token = JToken.Parse(marginData).SelectToken("assets");
                currency1Ret = findSymbolInUnnamedToken(token, currency1 + currency2);
                if (currency1Ret == "FAILED")
                {
                    return "0/0,0,0,0,0,0,0,0,0,0,0";
                }

                currency2Ret = JToken.Parse(currency1Ret).SelectToken("quoteAsset").ToString();
                currency1Ret = JToken.Parse(currency1Ret).SelectToken("baseAsset").ToString();
                //currency2Ret = findAssetInUnnamedToken(token, currency2);
                string totalBalance1 = JToken.Parse(currency1Ret).SelectToken("netAsset").ToString();
                string avail1 = JToken.Parse(currency1Ret).SelectToken("free").ToString();
                string borrowed1 = JToken.Parse(currency1Ret).SelectToken("borrowed").ToString();
                string interest1 = JToken.Parse(currency1Ret).SelectToken("interest").ToString();
                string totalBalance2 = JToken.Parse(currency2Ret).SelectToken("netAsset").ToString();
                string avail2 = JToken.Parse(currency2Ret).SelectToken("free").ToString();
                string borrowed2 = JToken.Parse(currency2Ret).SelectToken("borrowed").ToString();
                string interest2 = JToken.Parse(currency2Ret).SelectToken("interest").ToString();
                ret =
                    $"{currency1}/{currency2},{currency1},{borrowed1},{avail1},{currency2},{borrowed2},{avail2},{interest1},{interest2},{totalBalance1},{totalBalance2}";
                return ret;
            }
            catch (Exception e)
            {
                Logger.Error("Failed to parse JToken with Exception", e);
            }

            return ret;
        }

        public static string findSymbolInUnnamedToken(JToken token, string symbol)
        {
            string ret = "";
            token = token.First;
            if (token == null)
            {
                return "FAILED";
            }

            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == symbol)
                {
                    ret = token.ToString();
                    break;
                }

                token = token.Next;
            }

            return ret;
        }

        public static string findAssetInUnnamedToken(JToken token, string asset)
        {
            string ret = "";
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("asset").ToString() == asset)
                {
                    ret = token.ToString();
                    break;
                }

                token = token.Next;
            }

            return ret;
        }

        public static async Task<string> getMarginData(string api, string secret, bool isolated = true)
        {
            Logger.Info($"Calling getMarginData with isolated: {isolated}");

            string ha;
            if (isolated)
            {
                ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/margin/isolated/account", "s", api, secret, true);
            }
            else
            {
                ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/margin/account", "s", api, secret, true);
            }

            return ha;
        }

        public static async Task<string> getSpotData(string api, string secret)
        {
            Logger.Info($"Calling getSpotData");

            return await binanceHttpClass.CallAsync("GET", "api/v3/account", "s", api, secret, true);
        }

        public static async Task<string> getSwapPosMargin(string inst, string api, string secret)
        {
            string ret = await getSwapPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = (float.Parse(token.SelectToken("isolatedMargin").ToString()) -
                           float.Parse(token.SelectToken("unRealizedProfit").ToString())).ToString();
                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> getSwapPosLiq(string inst, string api, string secret)
        {
            string ret = await getSwapPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("liquidationPrice").ToString();
                    if (token.SelectToken("positionAmt").ToString().Contains('-'))
                    {
                        ret = "-" + ret;
                    }

                    if (token.SelectToken("positionAmt").ToString() == "0")
                    {
                        ret = "NOPOS";
                    }

                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> getSwapPosCost(string inst, string api, string secret)
        {
            string ret = await getSwapPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("entryPrice").ToString();
                    if (token.SelectToken("positionAmt").ToString().Contains('-'))
                    {
                        ret = "-" + ret;
                    }

                    if (token.SelectToken("positionAmt").ToString() == "0")
                    {
                        ret = "NOPOS";
                    }

                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static async Task<string> getSwapPosQty(string inst, string api, string secret)
        {
            string ret = await getSwapPositionData(api, secret);
            JToken token = JToken.Parse(ret);
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("symbol").ToString() == convertInstToBinance(inst))
                {
                    ret = token.SelectToken("positionAmt").ToString();
                    break;
                }

                token = token.Next;
                if (token == null)
                {
                    return "0";
                }
            }

            return ret;
        }

        public static string convertInstToBinance(string inst)
        {
            if (inst.Split('-').Length == 3) // Swap
            {
                inst = inst.Split('-')[0] + inst.Split('-')[1] + "_" + inst.Split('-')[2];
            }
            else if (inst.Length > 0)
            {
                inst = inst.Split('-')[0] + inst.Split('-')[1];
            }

            return inst;
        }

        public static async Task<string> changePosMarginSwap(string symbol, string amount, string type, string api,
            string secret)
        {
            Logger.Info($"Calling changePosMarginSwap with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);
            parameters.Set("amount", amount);
            parameters.Set("type", type);

            return await binanceHttpClass.CallAsync("POST", "fapi/v1/positionMargin", "Swap", api, secret, true,
                parameters);
        }

        public static async Task<string> ChangePosMarginSwapTryLoweringAmount(string instrument, decimal amount,
            string type, string api, string secret)
        {
            var initialAmount = amount;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    return await changePosMarginSwap(instrument, DecimalsHelper.ToString(amount), type, api, secret);
                }
                catch (BinanceErrorResponseException e)
                {
                    if (!e.Message.Contains("Isolated balance insufficient"))
                    {
                        throw;
                    }

                    amount *= 0.85m;
                }
            }

            throw new Exception(
                $"ChangePosMarginSwapTryLoweringAmount did not succeed after 5 attempts. Initial amount was {initialAmount}, final amount was: {amount}");
        }

        public static async Task<string> getSwapPositionData(string api, string secret)
        {
            Logger.Info($"Calling getSwapPositionData");

            return await binanceHttpClass.CallAsync("GET", "fapi/v1/positionRisk", "Swap", api, secret, true);
        }

        public static async Task<string> getTotal(string type, string api, string secret)
        {
            Logger.Info($"Calling getTotal with type :{type}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", type);

            return await binanceHttpClass.CallAsync("GET", "sapi/v1/accountSnapshot", "a", api, secret, true,
                parameters);
        }

        public static async Task<string> getSpotOpenOrders(string symbol, string api, string secret)
        {
            Logger.Info($"Calling getSpotOpenOrders with symbol :{symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            return await binanceHttpClass.CallAsync("GET", "api/v3/openOrders", "s", api, secret, true,
                parameters);
        }

        public static async Task<string> getMarginOpenOrders(string symbol, string api, string secret)
        {
            Logger.Info($"Calling getMarginOpenOrders for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);

            return await binanceHttpClass.CallAsync("GET", "sapi/v1/margin/openOrders", "a", api, secret, true,
                parameters);
        }

        public static async Task<string> getPIStopMargin(string inst, string api, string secret)
        {
            string line = "";
            string ret = await getMarginOpenOrders(inst, api, secret);
            JToken token = JToken.Parse(ret).First;
            if (token == null)
            {
                return "N/A,N/A,N/A";
            }

            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("type").ToString() == "STOP_LOSS_LIMIT")
                {
                    string stopSize = token.SelectToken("origQty").ToString();
                    string stopTrigger = token.SelectToken("stopPrice").ToString();
                    string stopPrice = token.SelectToken("price").ToString();
                    line = $"{stopSize},{stopTrigger},{stopPrice}";
                    break;
                }

                if (token.Next == null)
                {
                    line = "N/A,N/A,N/A";
                    break;
                }

                token = token.Next;
            }

            return line;
            //            quantity = token.SelectToken("avail_position").ToString();
            //            availFundsAmount = JToken.Parse(result).First.First.SelectToken("equity").ToString();
            //            maxTransferable = JToken.Parse(result).First.First.SelectToken("max_withdraw").ToString();
            //            longshort = token.SelectToken("side").ToString().Substring(0, 1).ToUpper() + token.SelectToken("side").ToString().Substring(1);
            //            avgPrice = token.SelectToken("avg_cost").ToString();
            //            leverage = token.SelectToken("leverage").ToString();
            //            liqPrice = token.SelectToken("liquidation_price").ToString();
            //            margin = token.SelectToken("margin").ToString();
        }

        public static async Task<string> getPIStopSpot(string inst, string api, string secret)
        {
            string line = "";
            string ret = await getSpotOpenOrders(inst, api, secret);
            JToken token = JToken.Parse(ret).First;
            if (token == null)
            {
                return "N/A,N/A,N/A";
            }

            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.SelectToken("type").ToString() == "STOP_LOSS_LIMIT")
                {
                    string stopSize = token.SelectToken("origQty").ToString();
                    string stopTrigger = token.SelectToken("stopPrice").ToString();
                    string stopPrice = token.SelectToken("price").ToString();
                    line = $"{stopSize},{stopTrigger},{stopPrice}";
                    break;
                }

                if (token.Next == null)
                {
                    line = "N/A,N/A,N/A";
                    break;
                }

                token = token.Next;
            }

            return line;
            //            quantity = token.SelectToken("avail_position").ToString();
            //            availFundsAmount = JToken.Parse(result).First.First.SelectToken("equity").ToString();
            //            maxTransferable = JToken.Parse(result).First.First.SelectToken("max_withdraw").ToString();
            //            longshort = token.SelectToken("side").ToString().Substring(0, 1).ToUpper() + token.SelectToken("side").ToString().Substring(1);
            //            avgPrice = token.SelectToken("avg_cost").ToString();
            //            leverage = token.SelectToken("leverage").ToString();
            //            liqPrice = token.SelectToken("liquidation_price").ToString();
            //            margin = token.SelectToken("margin").ToString();
        } //{buysell},{stopSize},{stopTrigger},{stopPrice},{availCryptoAmount},{availFundsAmount}

        public static async Task<string> getPIDataFutures(string inst, string api, string secret)
        {
            string line = "";
            string swapRet = await getFuturesData(api, secret);
            JToken assets = getFirstFromUntitled(JToken.Parse(swapRet).SelectToken("assets"),
                inst.Split('-')[0]);
            //string quantity = assets.SelectToken("")
            string pos = await getFuturesPositionData(api, secret);
            JToken positions = getFirstFromUntitled(JToken.Parse(pos), convertInstToBinance(inst));
            string quantity = positions.SelectToken("positionAmt").ToString();
            string availFundsAmount = assets.SelectToken("maxWithdrawAmount").ToString();
            string posExist = "No";
            if (double.Parse(quantity) != 0)
            {
                posExist = "Yes";
            }

            string maxTransferable = availFundsAmount;
            string avgPrice = positions.SelectToken("entryPrice").ToString();
            string leverage = positions.SelectToken("leverage").ToString();
            string liqPrice = positions.SelectToken("liquidationPrice").ToString();
            string margin = positions.SelectToken("isolatedMargin").ToString();
            line = $"{quantity},{margin},{leverage},{avgPrice},{liqPrice},{posExist},{availFundsAmount}";

            return line;
        }

        public static async Task<string> getPIDataSpot(string inst, string api, string secret)
        {
            string line = "";
            string swapRet = await getSpotData(api, secret);
            JToken token = JToken.Parse(swapRet).SelectToken("balances");
            JToken left = getFirstFromUntitled(token, inst.Split('-')[0]);
            string posSize = (double.Parse(left.SelectToken("free").ToString()) +
                              double.Parse(left.SelectToken("locked").ToString())).ToString();
            string quantity = left.SelectToken("free").ToString();
            JToken right = getFirstFromUntitled(token, inst.Split('-')[1]);
            string availFundsAmount = right.SelectToken("free").ToString();
            string maxTransferable = availFundsAmount;
            line = $"{posSize},{quantity},{availFundsAmount}";
            return line;
            //            quantity = token.SelectToken("avail_position").ToString();
            //            availFundsAmount = JToken.Parse(result).First.First.SelectToken("equity").ToString();
            //            maxTransferable = JToken.Parse(result).First.First.SelectToken("max_withdraw").ToString();
            //            longshort = token.SelectToken("side").ToString().Substring(0, 1).ToUpper() + token.SelectToken("side").ToString().Substring(1);
            //            avgPrice = token.SelectToken("avg_cost").ToString();
            //            leverage = token.SelectToken("leverage").ToString();
            //            liqPrice = token.SelectToken("liquidation_price").ToString();
            //            margin = token.SelectToken("margin").ToString();
        }

        public static async Task<string> getPIDataSwap(string inst, string api, string secret)
        {
            string line = "";
            string swapRet = await getSwapData(api, secret);
            JToken assets = getFirstFromUntitled(JToken.Parse(swapRet).SelectToken("assets"),
                inst.Split('-')[1].ToString());
            string pos = await getSwapPositionData(api, secret);
            JToken positions = getFirstFromUntitled(JToken.Parse(pos), convertInstToBinance(inst));
            string quantity = positions.SelectToken("positionAmt").ToString();
            string availFundsAmount = assets.SelectToken("maxWithdrawAmount").ToString();
            string maxTransferable = availFundsAmount;
            string avgPrice = positions.SelectToken("entryPrice").ToString();
            string leverage = positions.SelectToken("leverage").ToString();
            string liqPrice = positions.SelectToken("liquidationPrice").ToString();
            string margin = positions.SelectToken("isolatedMargin").ToString();
            line = $"{quantity},{availFundsAmount},{maxTransferable},{avgPrice},{leverage},{liqPrice},{margin}";
            return line;
            //            quantity = token.SelectToken("avail_position").ToString();
            //            availFundsAmount = JToken.Parse(result).First.First.SelectToken("equity").ToString();
            //            maxTransferable = JToken.Parse(result).First.First.SelectToken("max_withdraw").ToString();
            //            longshort = token.SelectToken("side").ToString().Substring(0, 1).ToUpper() + token.SelectToken("side").ToString().Substring(1);
            //            avgPrice = token.SelectToken("avg_cost").ToString();
            //            leverage = token.SelectToken("leverage").ToString();
            //            liqPrice = token.SelectToken("liquidation_price").ToString();
            //            margin = token.SelectToken("margin").ToString();
        }

        public static JToken getFirstFromUntitled(JToken token, string symbol)
        {
            token = token.First;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                if (token.First.First.ToString() == symbol)
                {
                    return token;
                }

                token = token.Next;
            }

            return token;
        }

        public static async Task<string> getAccountTotal(string api, string secret, string name, bool usdBased = false)
        {
            string line = "";
            string swapRet = await getSwapData(api, secret);
            string futuresRet = await getFuturesData(api, secret);
            string marginRet = await getMarginData(api, secret);
            string spotRet = await getSpotData(api, secret);
            double swapTotal = 0;
            double futuresTotal = 0;
            double spotTotal = 0;
            double marginTotal = 0;

            JToken token = JToken.Parse(swapRet).SelectToken("assets").First;
            string currency = "";
            string amount = "";
            string priceInUSD = "";
            swapTotal = await getExchangeTotal(token, "walletBalance", "asset", usdBased) +
                        await getExchangeTotal(token, "unrealizedProfit", "asset",
                            usdBased); // Need to check what "locked" means
            //string[] FB = new FunctionClass().getFB();
            //for(int i = 1; i < FB.Length; i++)
            //{
            //    if (FB[i].Split(',')[0] == name)
            //    {
            //        swapTotal += (double.Parse(FB[i].Split(',')[1]) * double.Parse(await getSpotPrice("BTC-USDT")));
            //        break;
            //    }
            //}
            double swapAvail = await getExchangeTotal(token, "marginBalance", "asset", true) -
                               await getExchangeTotal(token, "initialMargin", "asset", true);
            token = JToken.Parse(futuresRet).SelectToken("assets").First;
            futuresTotal = await getExchangeTotal(token, "marginBalance", "asset", usdBased);
            double hodlTotal = 0;
            token = JToken.Parse(futuresRet).SelectToken("positions").First;
            //if (hodlFuturesInsts != null)
            //{
            //    hodlTotal += await getFuturesTotalForInstruments(token, hodlFuturesInsts,usdBased);
            //}
            token = JToken.Parse(spotRet).SelectToken("balances").First;
            // token can be null if balances are an empty array.
            if (token != null)
            {
                spotTotal = await getExchangeTotal(token, "free", "asset", usdBased, "locked");
            }

            token = JToken.Parse(marginRet).SelectToken("totalNetAssetOfBtc");
            if (token != null)
            {
                // they changed it to give me it right away so im using it cuz way easier
                marginTotal =
                    double.Parse(token
                        .ToString()); //await getExchangeTotal(token, "totalAsset", "asset", usdBased, "", true);
                if (usdBased)
                {
                    marginTotal *= double.Parse(await getSpotPrice("BTC-USDT"));
                }
            }

            double totalUsd = 0;
            double totalBtc = 0;
            if (usdBased)
            {
                totalUsd = marginTotal + swapTotal + spotTotal + futuresTotal;
                totalBtc = totalUsd / double.Parse(await getSpotPrice("BTC-USDT"));
            }
            else
            {
                totalBtc = marginTotal + swapTotal + spotTotal + futuresTotal;
                totalUsd = totalBtc * double.Parse(await getSpotPrice("BTC-USDT"));
            }

            line = $"{totalBtc}"; //,{hodlTotal},{totalUsd},{swapAvail}";
            return line;
        }

        public static async Task<List<JToken>> getSwapCandle(string symbol)
        {
            Logger.Info($"Calling getSwapCandle for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "fapi/v1/klines", "Futures", "", "", false,
                parameters);
            return JToken.Parse(ha).Reverse().ToList();
        }

        public static async Task<List<JToken>> getMarginCandle(string symbol)
        {
            Logger.Info($"Calling getMarginCandle for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("interval", "1h");
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/klines", "Margin", "", "", false, parameters);
            return JToken.Parse(ha).Reverse().ToList();
        }

        public static async Task<double> getFuturesTotalForInstruments(JToken token, string[][] instruments,
            bool usdBased)
        {
            double totalAum = 0;
            while (token != null)
            {
                for (int i = 0; i < instruments.Length; i++)
                {
                    if (token.SelectToken("symbol").ToString() == convertInstToBinance(instruments[i][1]))
                    {
                        if (usdBased == false)
                        {
                            if (instruments[i][1].Split('-')[0] == "BTC")
                            {
                                totalAum += double.Parse(token.SelectToken("unrealizedProfit").ToString());
                            }
                            else
                            {
                                totalAum += double.Parse(token.SelectToken("unrealizedProfit").ToString()) /
                                            double.Parse(
                                                await getSpotPrice(instruments[i][1].Split('-')[0] + "-" + "BTC"));
                            }
                        }
                        else
                        {
                            // aum is in base currency
                            totalAum += double.Parse(token.SelectToken("unrealizedProfit").ToString()) /
                                        double.Parse(
                                            await getSpotPrice(instruments[i][1].Split('-')[0] + "-" + "USDT"));
                        }
                    }

                    token = token.Next;
                }
            }

            return totalAum;
        }

        public static async Task<double> getExchangeTotal(JToken token, string netAsset, string currencyAsset,
            bool usdBased, string locked = "", bool isolatedMargin = false)
        {
            double totalAUM = 0;
            for (int i = 0; i < token.Parent.Count(); i++)
            {
                string currency = "";
                string amount = "0";
                string amountQ = "0";
                string currencyQ = "";
                if (isolatedMargin)
                {
                    currency = token.SelectToken("baseAsset").SelectToken(currencyAsset).ToString();
                    amount = token.SelectToken("baseAsset").SelectToken(netAsset).ToString();
                    currencyQ = token.SelectToken("quoteAsset").SelectToken(currencyAsset).ToString();
                    amountQ = token.SelectToken("quoteAsset").SelectToken(netAsset).ToString();
                    if (amount != "0" || amountQ != "0")
                    {
                    }
                }
                else
                {
                    currency = token.SelectToken(currencyAsset).ToString();
                    amount = token.SelectToken(netAsset).ToString();
                }

                string lockedAmount = "0";
                if (locked != "")
                {
                    lockedAmount = token.SelectToken(locked).ToString();
                }

                if (double.Parse(amount) != 0)
                {
                    if (usdBased)
                    {
                        if (currency == "USD" || currency == "USDT")
                        {
                            if (lockedAmount != "0")
                            {
                                totalAUM += double.Parse(lockedAmount);
                            }

                            totalAUM += double.Parse(amount); // ammount is in dollars
                        }
                        else
                        {
                            string priceInUSD = await getSpotPrice($"{currency}-USDT");
                            if (lockedAmount != "0")
                            {
                                totalAUM += double.Parse(lockedAmount) * double.Parse(priceInUSD);
                            }

                            totalAUM += double.Parse(amount) * double.Parse(priceInUSD);
                        }
                    }
                    else // getting aum based on base currency
                    {
                        if (currency == "USD" || currency == "USDT")
                        {
                            string priceInBTC = await getSpotPrice($"BTC-USDT");
                            if (lockedAmount != "0")
                            {
                                totalAUM += double.Parse(lockedAmount);
                            }

                            totalAUM += double.Parse(amount) / double.Parse(priceInBTC); // ammount is in dollars
                        }
                        else if (currency == "BTC")
                        {
                            if (lockedAmount != "0")
                            {
                                totalAUM += double.Parse(lockedAmount);
                            }

                            totalAUM += double.Parse(amount); // ammount is in BTC
                        }
                        else
                        {
                            string priceInBTC = await getSpotPrice($"{currency}-BTC");
                            if (lockedAmount != "0")
                            {
                                totalAUM += double.Parse(lockedAmount) * double.Parse(priceInBTC);
                            }

                            totalAUM += double.Parse(amount) * double.Parse(priceInBTC);
                        }
                    }
                }

                if (double.Parse(amountQ) != 0)
                {
                    if (usdBased)
                    {
                        if (currencyQ == "USD" || currencyQ == "USDT"
                        ) // if its isolated margin we check for both quote asset and base asset
                        {
                            totalAUM += double.Parse(amountQ); // ammount is in dollars
                        }
                        else if (isolatedMargin)
                        {
                            string priceInUSD = await getSpotPrice($"{currencyQ}-USDT");
                            totalAUM += double.Parse(amountQ) * double.Parse(priceInUSD);
                        }
                    }
                    else
                    {
                        if (currencyQ == "BTC") // if its isolated margin we check for both quote asset and base asset
                        {
                            totalAUM += double.Parse(amountQ); // ammount is in BTC
                        }
                        else if (isolatedMargin)
                        {
                            string priceInBTC = await getSpotPrice($"{currencyQ}-BTC");
                            totalAUM += double.Parse(amountQ) * double.Parse(priceInBTC);
                        }
                    }
                }

                if (token.Next != null)
                {
                    token = token.Next;
                }
            }

            return totalAUM;
        }

        public static async Task<string> getPrice(string inst, string exchange, string openOrClose = "Close")
        {
            if (inst != "N/A")
            {
                if (openOrClose == "Close")
                {
                    if (exchange == "Swap")
                    {
                        return await getSwapPrice(inst);
                    }

                    if (exchange == "Futures")
                    {
                        return await getFuturesPrice(inst);
                    }

                    if (exchange == "Margin")
                    {
                        return await getMarginPrice(inst);
                    }

                    if (exchange == "Spot")
                    {
                        return await getSpotPrice(inst);
                    }
                }
                else if (openOrClose == "Open")
                {
                    List<JToken> data = null;
                    if (exchange == "Swap")
                    {
                        data = await getSwapCandle(inst);
                    }

                    if (exchange == "Futures")
                    {
                        data = await getFuturesCandle(inst);
                    }

                    if (exchange == "Margin")
                    {
                        data = await getMarginCandle(inst);
                    }

                    if (exchange == "Spot")
                    {
                        data = await getSpotCandle(inst);
                    }

                    var line = "";
                    for (int i = 0; i < 24; i++)
                    {
                        var hour = data[i].ToList();
                        var candleStartTime = long.Parse(hour[0].ToString());
                        var hourDatetime = UnixTimeStampToDateTime(candleStartTime).ToLocalTime();
                        if (hourDatetime.Hour == 21)
                        {
                            // opening price
                            line = hour[1].ToString();
                            break;
                        }
                    }

                    if (line == "") line = "failed";

                    return line;
                }
            }

            return "";
        }

        public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp / 1000);
            return dtDateTime;
        }

        public static async Task<string> getSpotPrice(string symbol)
        {
            Logger.Info($"Calling getSpotPrice for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "api/v3/ticker/price", "a", "", "", false, parameters);
            try
            {
                return JToken.Parse(ha).SelectToken("price").ToString();
            }
            catch (Exception e)
            {
                Logger.Error("failed to parse response from getSpotPrice", e);
                return "0";
            }
        }

        public static async Task<string> getSwapPrice(string symbol)
        {
            Logger.Info($"Calling getSwapPrice for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "fapi/v1/ticker/price", "Swap", "", "", false,
                parameters);
            return JToken.Parse(ha).SelectToken("price").ToString();
        }

        public static async Task<string> getFuturesInfo()
        {
            Logger.Info($"Calling getFuturesInfo");

            return await binanceHttpClass.CallAsync("GET", "dapi/v1/exchangeInfo", "Futures", "", "", false);
        }

        public static async Task<string> getMarginPrice(string symbol)
        {
            Logger.Info($"Calling getSwapPrice for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string ha = await binanceHttpClass.CallAsync("GET", "sapi/v3/ticker/price", "a", "", "", false,
                parameters);
            return JToken.Parse(ha).SelectToken("price").ToString();
        }

        public static async Task<string> getSpotBalance(string api, string secret)
        {
            Logger.Info($"Calling getSpotBalance");

            return await binanceHttpClass.CallAsync("GET", "api/v3/account", "s", api, secret, true);
        }

        public static async Task<string> getSwapData(string api, string secret)
        {
            Logger.Info($"Calling getSwapData");

            return await binanceHttpClass.CallAsync("GET", "fapi/v1/account", "Swap", api, secret, true);
        }

        public static async Task<string> getSwapBalance(string api, string secret)
        {
            return await binanceHttpClass.CallAsync("GET", "fapi/v1/balance", "Swap", api, secret, true);
        }


        public static async Task<string> cancelOrdersSpot(string symbol, string api, string secret)
        {
            Logger.Info($"Calling cancelOrdersSpot for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            try
            {
                return await binanceHttpClass.CallAsync("DEL", "api/v3/openOrders", "s", api, secret, true,
                    parameters);
            }
            catch (BinanceErrorResponseException e)
            {
                if (!e.Message.Contains("Unknown order sent."))
                {
                    throw;
                }

                // Binance returns a BadRequest if there are no open orders, for now we can ignore this exception
                Logger.Warn("cancelOrdersSpot threw the 'Unknown order sent.' error. Ignore it for now.");
            }

            return "";
        }

        public static async Task<string> cancelOrdersMargin(string symbol, string api, string secret)
        {
            Logger.Info($"Calling cancelOrdersMargin for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);

            try
            {
                string ha = await binanceHttpClass.CallAsync("DEL", "sapi/v1/margin/openOrders", "s", api, secret, true,
                    parameters);
                return ha;
            }
            catch (BinanceErrorResponseException e)
            {
                if (!e.Message.Contains("Unknown order sent."))
                {
                    throw;
                }

                // Binance returns a BadRequest if there are no open orders, for now we can ignore this exception
                Logger.Warn("cancelOrdersMargin threw the 'Unknown order sent.' error. Ignore it for now.");
            }

            return "";
        }

        public static async Task<string> cancelOrdersSwap(string symbol, string api, string secret)
        {
            Logger.Info($"Calling cancelOrdersSwap for symbol {symbol}");
            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("symbol", symbol);

            string paramString = $"symbol={symbol}";
            string ha = await binanceHttpClass.CallAsync("DEL", "fapi/v1/allOpenOrders", "Swap", api, secret, true,
                parameters);
            return ha;
        }

        public static async Task<double> MaxBorrowable(string isolatedSymbol, string asset, string api, string secret)
        {
            Logger.Info($"Calling MaxBorrowable for isolatedSymbol: {isolatedSymbol}, asset: {asset}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("asset", asset);
            parameters.Set("isolatedSymbol", isolatedSymbol);

            string ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/margin/maxBorrowable", "s", api, secret, true,
                parameters);
            var res = JsonConvert.DeserializeObject<dynamic>(ha);
            /*
             * {
                  "amount": "1.69248805", // account's currently max borrowable amount with sufficient system availability
                  "borrowLimit": "60" // max borrowable amount limited by the account level
                }
             */
            string amount = res["amount"];

            return Convert.ToDouble(amount);
        }

        public static async Task<string> orderIcebergSpot(string symbol, string side, string quantity, string price,
            string icebergQty, string api, string secret)
        {
            Logger.Info($"Calling orderIcebergSpot for symbol: {symbol}, side: {side}, quantity: {quantity}, price: {price}, icebergQty: {icebergQty}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "LIMIT");
            parameters.Set("timeInForce", "GTC");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);
            parameters.Set("price", price);
            parameters.Set("icebergQty", icebergQty);

            return await binanceHttpClass.CallAsync("POST", "api/v3/order", "s", api, secret, true, parameters);
        }
        //public static async Task<string> orderIcebergMargin(string symbol, string side, string quantity, string price, string icebergQty, string api, string secret)
        //{
        //    string paramString = $"symbol={symbol}&side={side}&type=LIMIT&quantity={quantity}&price={price}&icebergQty={icebergQty}&timeInForce=GTC";
        //    string ha = await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/order", "s", api, secret, true, paramString);
        //    return ha;
        //}

        public static async Task<string> orderStopMargin(string symbol, string side, string quantity, string price,
            string stopPrice, string api, string secret)
        {
            Logger.Info($"Calling orderStopMargin for symbol: {symbol}, side: {side}, quantity: {quantity}, price: {price}, stopPrice: {stopPrice}");

            if (side == "2" || side == "3")
            {
                side = "SELL";
            }

            if (side == "1" || side == "4")
            {
                side = "BUY";
            }

            int quantityRounding = 5;
            int priceRounding = 5;
            price = Math.Round(float.Parse(price), priceRounding + 2).ToString("F8");
            stopPrice = Math.Round(float.Parse(stopPrice), priceRounding + 2).ToString("F8");
            quantity = Math.Floor(float.Parse(quantity)).ToString("F" + (3 + quantityRounding));

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "STOP_LOSS_LIMIT");
            parameters.Set("timeInForce", "GTC");
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);
            parameters.Set("price", price);
            parameters.Set("stopPrice", stopPrice);

            while (quantityRounding > 0 && priceRounding > 0)
            {
                try
                {
                    // will return once successful
                    return await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/order", "s", api, secret, true,
                        parameters);
                }
                catch (BinanceErrorResponseException e)
                {
                    if (!(e.Message.Contains("LOT") || e.Message.Contains("unable")) && !(e.Message.Contains("PRICE") || e.Message.Contains("unable")))
                    {
                        throw;
                    }

                    if (e.Message.Contains("LOT") || e.Message.Contains("unable"))
                    {
                        Logger.Warn("in orderStopMargin got error that quantity is too big, trying to round to a higher decimal");

                        quantityRounding -= 1;
                        quantity = Math.Floor(float.Parse(quantity)).ToString("F" + (3 + quantityRounding));
                        parameters.Set("quantity", quantity);
                    }

                    if (e.Message.Contains("PRICE") || e.Message.Contains("unable"))
                    {
                        Logger.Warn("in orderStopMargin got error that price is too high, trying to round to a higher decimal");

                        priceRounding -= 1;
                        price = Math.Round(float.Parse(price), 3 + 2).ToString("F" + (3 + priceRounding));
                        stopPrice = Math.Round(float.Parse(stopPrice), 3 + 2).ToString("F" + (3 + priceRounding));
                        parameters.Set("price", price);
                        parameters.Set("stopPrice", stopPrice);
                    }

                    if (quantityRounding == 0 || priceRounding == 0)
                    {
                        Logger.Error("in orderStopMargin Failed to round up enough after 5 iterations");
                        throw;
                    }
                }
            }

            // This return should be unreachable
            return "";
        }

        public static async Task<string> orderMarketMargin(string symbol, string side, string quantity, string api,
            string secret)
        {
            Logger.Info($"Calling orderMarketMargin for symbol: {symbol}, side: {side}, quantity: {quantity}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("type", "MARKET");
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);
            parameters.Set("side", side);
            parameters.Set("quantity", quantity);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/order", "", api, secret, true,
                parameters);
        }

        public static async Task<string> getLoanDataSwap(string api, string secret)
        {
            Logger.Info($"Calling getLoanDataSwap");

            string ha = await binanceHttpClass.CallAsync("GET", "sapi/v1/futures/loan/wallet", "", api, secret, true);
            ha = JToken.Parse(ha).SelectToken("totalBorrowed") + "," +
                 JToken.Parse(ha).SelectToken("crossCollaterals").First.SelectToken("locked") + "," + JToken
                     .Parse(ha).SelectToken("crossCollaterals").First.SelectToken("currentCollateralRate");

            return ha;
        }

        public static async Task<string> marginLoan(string symbol, string asset, string amount, string api, string secret)
        {
            Logger.Info($"Calling marginLoan for symbol: {symbol}, asset: {asset}, amount: {amount}");

            if (amount == "0")
            {
                Logger.Warn("Attempting to send a marginLoan request with '0' amount, aborting request");
                return "";
            }

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);
            parameters.Set("asset", asset);
            parameters.Set("amount", amount);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/loan", "", api, secret, true,
                parameters);
        }

        public static async Task<string> marginRepay(string symbol, string asset, string amount, string api,
            string secret)
        {
            Logger.Info($"Calling marginRepay for symbol: {symbol}, asset: {asset}, amount: {amount}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("isIsolated", "TRUE");
            parameters.Set("symbol", symbol);
            parameters.Set("asset", asset);
            parameters.Set("amount", amount);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/repay", "", api, secret, true,
                parameters);
        }

        /// <summary>the type should be in this format: spotMargin - will transfer from spot to margin and must send inst when doing margin stuff</summary>
        public static async Task<string> transfer(string currency, string amount, string type, string api,
            string secret, string inst = "")
        {
            string ret = "";
            if (type == "spotMargin")
            {
                ret = await transferSpotMargin(amount, currency, inst, "SPOT", "ISOLATED_MARGIN", api, secret);
            }

            if (type == "spotSwap")
            {
                ret = await transferSpotSwap(amount, currency, "1", api, secret);
            }

            if (type == "spotFutures")
            {
                ret = await transferSpotSwap(amount, currency, "3", api, secret);
            }

            if (type == "futuresSpot")
            {
                ret = await transferSpotSwap(amount, currency, "4", api, secret);
            }

            if (type == "marginSpot")
            {
                ret = await transferSpotMargin(amount, currency, inst, "ISOLATED_MARGIN", "SPOT", api, secret);
            }

            if (type == "swapSpot")
            {
                ret = await transferSpotSwap(amount, currency, "2", api, secret);
            }

            if (type == "swapSpot")
            {
                ret = await transferSpotSwap(amount, currency, "4", api, secret);
            }

            if (type == "marginSwap")
            {
                ret = await transferSpotMargin(amount, currency, inst, "ISOLATED_MARGIN", "SPOT", api, secret);
                // transfering form margin to spot and then from spot to Swap
                await transferSpotSwap(amount, currency, "1", api, secret);
            }

            if (type == "marginFutures")
            {
                ret = await transferSpotMargin(amount, currency, inst, "ISOLATED_MARGIN", "SPOT", api, secret);
                // transfering form margin to spot and then from spot to Swap
                await transferSpotSwap(amount, currency, "3", api, secret);
            }

            if (type == "swapMargin")
            {
                ret = await transferSpotSwap(amount, currency, "2", api, secret);
                ret = await transferSpotMargin(amount, currency, inst, "SPOT", "ISOLATED_MARGIN", api, secret);
            }

            if (type == "swapMargin")
            {
                ret = await transferSpotSwap(amount, currency, "4", api, secret);
                ret = await transferSpotMargin(amount, currency, inst, "SPOT", "ISOLATED_MARGIN", api, secret);
            }

            return ret;
        }

        public static async Task<string> transferSpotSwap(string amount, string asset, string type, string api,
            string secret)
        {
            Logger.Info($"Calling transferSpotSwap for amount: {amount}, asset: {asset}, type: {type}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("asset", asset);
            parameters.Set("amount", amount);
            parameters.Set("type", type); //types - 3 = spot to Swap 4 = Swap to spot

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/futures/transfer", "Reg", api, secret, true,
                parameters);
        }

        /// <summary>type 1 is from spot to margin type 2 is from margin to spot</summary>
        public static async Task<string> transferSpotMargin(string amount, string asset, string symbol,
            string transferFrom, string transferTo, string api, string secret)
        {
            Logger.Info(
                $"Calling transferSpotMargin for amount: {amount}, asset: {asset}, symbol: {symbol}, transferFrom: {transferFrom}, transferTo: {transferTo}");

            var parameters = HttpUtility.ParseQueryString(string.Empty);
            parameters.Set("asset", asset);
            parameters.Set("amount", amount);
            parameters.Set("transFrom", transferFrom);
            parameters.Set("transTo", transferTo);
            parameters.Set("symbol", symbol);

            return await binanceHttpClass.CallAsync("POST", "sapi/v1/margin/isolated/transfer", "Reg", api, secret,
                true, parameters);
        }

        //public static async Task<string> orderMarketPrep(string symbol, string side, string quantity, string api, string secret)
        //{
        //    string paramString = $"symbol={symbol}&side={side}&type=MARKET&quantity={quantity}";
        //    string ha = await HttpClass.CallAsync("POST", "fapi/v1/order", "Swap", api, secret, true, paramString);
        //    return ha;
        //}
        //public static async Task<string> orderMarketPrep(string symbol, string side, string quantity, string api, string secret)
        //{
        //    string paramString = $"symbol={symbol}&side={side}&type=MARKET&quantity={quantity}";
        //    string ha = await HttpClass.CallAsync("POST", "fapi/v1/order", "Swap", api, secret, true, paramString);
        //    return ha;
        //}
    }
}