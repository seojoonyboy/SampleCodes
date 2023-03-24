const { error } = require('console');
const mysql = require('mysql2/promise');
const cheerio = require('cheerio');
const axios = require('axios');
const async = require('async');
const moment = require('moment');

let db;

const EXCHANGE_URLS = {
    NAVER: {
        USD : 'https://finance.naver.com/marketindex/exchangeDailyQuote.nhn?marketindexCd=FX_USDKRW',
        JPY : 'https://finance.naver.com/marketindex/exchangeDailyQuote.nhn?marketindexCd=FX_JPYKRW'
    },
    INVESTINGDOTCOM: {
        USD : 'https://kr.investing.com/currencies/usd-krw',
        JPY : 'https://kr.investing.com/currencies/jpy-krw'
    }
}


async function run(){
    try{
        console.time('runtime');

        db = await mysql.createPool({
            connectionLimit: 10,
            host: '',
            user: '',
            password: '',
        });

        if(!db) throw new Error('db connect failed');

        const exchangeData = await crawlExchange();
        await insertExhangeDataToDB(exchangeData['Date'], exchangeData['USD'], exchangeData['JPY']);

        console.timeEnd('runtime');
    }
    catch(error){
        console.error(error);
    }
}


async function crawlExchange(){
    return new Promise((resolve, reject) => {
        let exchange = {};
        async.waterfall([
            (next) => {
                exchange['Date'] = moment()
                    .toISOString()
                    .substring(0,10);

                console.log(exchange['Date']);
                next(null, exchange['Date']);
            },
            (date, next) => {
                //달러 정보 크롤링
                const url = EXCHANGE_URLS.INVESTINGDOTCOM.USD;

                axios.get(url).then((res) => {
                    //exchange['USD'] = GetDataFromNaver(res, exchange['Date']);
                    exchange['USD'] = GetDataFromInvestingDotCom(res);

                    console.log('USD Searched ExchangeValue : ' + exchange['USD']);

                    next(null, exchange['Date']);
                });
            },
            (date, next) => {
                //엔화 정보 크롤링
                const url = EXCHANGE_URLS.INVESTINGDOTCOM.JPY;

                axios.get(url).then((res) => {
                    // const target_value = (GetDataFromNaver(res, exchange['Date']) / 100).toFixed(4);
                    exchange['JPY'] = GetDataFromInvestingDotCom(res);

                    console.log('JPY Searched ExchangeValue : ' + exchange['JPY']);

                    next(null, exchange);
                });
            }
        ], (err, result) => {
            if(err) reject(err);
            resolve(result);
        });
    });
}


async function insertExhangeDataToDB(date, USD, JPY){
    console.log('INSERT INTO dalcomtools.ExchangeRate...');
    console.log(`${date} : USD : ${USD}, JPY : ${JPY}`);
    if(!USD || !JPY) throw new Error('Exchange Value Load Failed');

    const qry = mysql.format(`
        INSERT INTO dalcomtools.ExchangeRate (
            date, USD, JPY
        ) VALUES (
            ?, ?, ?
        )`, [date, USD, JPY]
    );
    try{
        await db.query(qry);
    }
    catch(e){
        console.log(e);
    }
}


//Naver(하나은행) 환율 값 크롤링
function GetDataFromNaver(res, date){
    const $ = cheerio.load(res.data);
    const tbody = $('tbody');
    const tds = tbody
        .find('tr')
        .children('td')
        .filter('.date');

    let target_tr = null;
    $(tds).each(function(i, elem){
        if(elem.children[0].data == date.replace(/\-/gi, '.')){
            target_tr = $(this).parent();
        }
    });
    if(!target_tr){
        reject(new Error('USD ExchangeValue Not Found'));
    }
    const target_value_text = $(target_tr)
        .children('.num')
        .first()
        .text();
    const target_value = parseFloat(target_value_text.replace(',', ''));
    return target_value;
}


//Investing.com 환율 값 크롤링
function GetDataFromInvestingDotCom(res){
    const $ = cheerio.load(res.data);
    const div_kcrypt = $('div#quotes_summary_current_data');
    const value_text = $(div_kcrypt)
        .find('#last_last')
        .text();
    const target_value = parseFloat(value_text.replace(',', ''));
    return target_value;
}


module.exports = run;
