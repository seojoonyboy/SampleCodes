process.env.TZ = 'Asia/Seoul';

const mysql = require('mysql2/promise');
const Async = require('async');
const moment = require('moment');

const CONFIG = require('./config.js');

async function query_1(connection, db_name, dateRange) {
    const b = moment(dateRange.begin).format('YYYY-MM-DD hh:mm:ss');
    const e = moment(dateRange.end).format('YYYY-MM-DD hh:mm:ss');

    try {
        await connection.execute(`SET @BEGIN = '${b}'`);
        await connection.execute(`SET @END = '${e}'`);
        const [rows] = await connection.query(`
        select *
        from (
            select
                objectID,
                owner,
                missionCode
            from
                ${db_name}.MissionProgress progress
            where
                createdAt between @BEGIN and @END
                and missionCode IN ('1648', '1659', '1670', '1681', '1692')
                and status = 2
        ) A
        order by owner
        `);
        return rows;
    } catch (e) {
        console.error(e);
    }
}

async function query_2(connection, objectIDs, dateRange, db_name) {
    const b = moment(dateRange.begin).format('YYYY-MM-DD hh:mm:ss');
    const e = moment(dateRange.end).format('YYYY-MM-DD hh:mm:ss');

    let result = {};
    try {
        await connection.execute(`SET @BEGIN = '${b}'`);
        await connection.execute(`SET @END = '${e}'`);

        //ss_gfriend_account_1

        const [rows] = await connection.query(`
            select *
            from (
                select
                    objectID,
                    owner,
                    missionProgressID
                from
                    ${db_name}.MissionTransaction progress
                where
                    createdAt between @BEGIN and @END
                    and status = 1
                    and missionProgressID in (?)
            ) A
            order by owner
        `, [objectIDs]);


        rows.forEach(row => {
            const owner = row.owner;
            const missionProgressID = row.missionProgressID;

            if(!result[owner]){
                result[owner] = {
                    count : 0,
                    owner : owner,
                    missionProgressIDs : []
                }
            }

            result[owner].count++;
            result[owner].missionProgressIDs.push(missionProgressID);
        });
        return result;
    } catch (e) {
        console.error(e);
    }
}

async function query_3(connection, reports, dateRange, db_name) {
    const b = moment(dateRange.begin).format('YYYY-MM-DD hh:mm:ss');
    const e = moment(dateRange.end).format('YYYY-MM-DD hh:mm:ss');

    const owners = Object.keys(reports);

    try {
        let result = {};
        Object.values(reports).forEach(report => {
            if(report.count >= 5){
                const owner = report.owner;
                result[owner] = {
                    device : undefined,
                    nickname : undefined,
                    email : undefined,
                    count : report.count,
                    owner : owner
                }
            }
        });

        await connection.execute(`SET @BEGIN = '${b}'`);
        await connection.execute(`SET @END = '${e}'`);

        //ss_gfriend_account_1

        const [user_rows] = await connection.query(`
            select device, objectID
            from ${db_name}.User u
            where u.objectID in (?)
        `, [owners]);

        user_rows.forEach(user_row => {
            if(result[user_row.objectID]){
                result[user_row.objectID].device = user_row.device;
            }
        });

        const [shard_rows] = await connection.query(`
            select nickname, userObjectID
            from ss_master.ShardMap shard
            where shard.userObjectID in (?)
        `, [owners]);

        shard_rows.forEach(shard_row => {
            if(result[shard_row.userObjectID]){
                result[shard_row.userObjectID].nickname = shard_row.nickname;
            }
        });

        const [sns_rows] = await connection.query(`
            select email, userObjectID
            from ss_master.SnsMap sns
            where sns.userObjectID in (?)
        `, [owners]);

        sns_rows.forEach(sns_row => {
            if(result[sns_row.userObjectID]){
                result[sns_row.userObjectID].email = sns_row.email;
            }
        });

        return result;
    } catch (e) {
        console.error(e);
    }
}

async function main() {
    console.time("calculate_exec_time");

    const app = 'SSG';

    const CFG = CONFIG[app];
    const connection = await mysql.createConnection({
        host: CFG.DB.host,
        port: CFG.DB.port,
        user: CFG.DB.user,
        password: CFG.DB.password,
    });
    await connection.connect();

    const dateRange = {begin : '2021-06-15 14:00:00', end : '2021-06-22 12:00:00'};

    let reports_account_1 = {};
    let reports_account_2 = {};

    const account_1_query1_result = await query_1(connection, 'account_1', dateRange);
    const account_2_query1_result = await query_1(connection, 'account_2', dateRange);

    account_1_query1_result.forEach(row => {
        const owner = row.owner;
        const objectID = row.objectID;
        const missionCode = row.missionCode;

        if(!reports_account_1[owner]) {
            reports_account_1[owner] = {
                clearList : [],
                owner : owner,
                objectID : []
            }
        }

        reports_account_1[owner].objectID.push(objectID);
        reports_account_1[owner].clearList.push(missionCode);
    });

    account_2_query1_result.forEach(row => {
        const owner = row.owner;
        const objectID = row.objectID;
        const missionCode = row.missionCode;

        if(!reports_account_2[owner]) {
            reports_account_2[owner] = {
                clearList : [],
                owner : owner,
                objectID : []
            }
        }

        reports_account_2[owner].objectID.push(objectID);
        reports_account_2[owner].clearList.push(missionCode);
    });

    //추가 필터 : 미션 5개를 모두 완료한 경우
    reports_account_1 = Object.values(reports_account_1).filter(x => (x.clearList.length >= 5));
    reports_account_2 = Object.values(reports_account_2).filter(x => (x.clearList.length >= 5));

    //추가 필터 : 보상을 모두 수령한 경우
    let account_1_objectIDs = [];
    reports_account_1.forEach(report => {
        var arr = report["objectID"];

        arr.forEach(objectID => {
            account_1_objectIDs.push(objectID);
        });
    });

    let account_2_objectIDs = [];
    reports_account_2.forEach(report => {
        var arr = report["objectID"];

        arr.forEach(objectID => {
            account_2_objectIDs.push(objectID);
        });
    });

    const account_1_query2_result = await query_2(connection, account_1_objectIDs, dateRange, 'account_1');
    const account_2_query2_result = await query_2(connection, account_2_objectIDs, dateRange, 'account_2');

    //TODO
    //count도 추가...
    //owner값도 추가...
    const account_1_query3_result = await query_3(connection, account_1_query2_result, dateRange, 'account_1');
    const account_2_query3_result = await query_3(connection, account_2_query2_result, dateRange, 'account_2');

    //owner값을 이용하여
    let total_reports = [];
    total_reports.push(...Object.values(account_1_query3_result));
    total_reports.push(...Object.values(account_2_query3_result));

    let headers = `ObjectID\tnickname\temail\tdevice\tcount`;
    console.log(headers);
    total_reports = total_reports.sort(x => x.owner);
    total_reports.forEach(report => {
        console.log(`${report.owner}\t${report.nickname}\t${report.email}\t${report.device}\t${report.count}`);
    });

    await connection.end();

    console.timeEnd("calculate_exec_time");
}

main();
