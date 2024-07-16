let dailyReportTable = null;
let issueInput = null;
const dailyReportOptions = {
    data: [],
    columns: [],
    width: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    rowHeights: 20,
    colWidths: [85, 105, 105, 95, 95, 95, 95, 95, 95, 95],
    nestedHeaders: [[]],
    fixedColumnsLeft: 1,
    licenseKey: 'non-commercial-and-evaluation',
    cells: (row, col) => ({
        type: (col === 0 || (row === 0 && col > 2)) ? 'text' : 'numeric',
        wordWrap: false,
        renderer (instance, td, row, col, prop, value, cellProperties) {
            if (col === 0) {
                Handsontable.renderers.TextRenderer.apply(this, arguments);
                td.style.color = '#000';
                td.style.background = '#f0f0f0';
            } else if (row === 0) {
                Handsontable.renderers.TextRenderer.apply(this, arguments);
                if (col > 2 && !value) {
                    td.innerHTML = '-';
                }
            } else {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (col === 1) {
                    td.style.background = '#fff2cc';
                }
            }
        }
    }),
    
    afterChange: (list, action) => {
        if (action === 'edit') {
            const year = moment().format("YYYY");
            list.forEach(data => {
                //월이라고 표기되어 있는 곳은 조작하는 곳이 아닌 읽기 전용
                if(data[1].includes('/')){
                    var date = '20' + data[1].replaceAll('/', '-');
                    date = date.split('(')[0];

                    //todo : 2020 2120 등의 년도 구분 필요
                    const comment = data[3];
                
                    $.ajax({
                        url: `/business/kpi/dailyreports/comment`,
                        method: 'POST',
                        dataType: 'json',
                        data: { comment, date }
                    }).done((data) => {
                        if (data.success) {
                            // 성공
                        } else {
                            console.log(data);
                        }
                    }).fail((xhr, status, errorThrown) => {
                        if (xhr.readyState === 4) {
                        } else {
                        }
                    });
                }
            });
        }
    }
};

function dailyReportTableLoad() {
    const end = $('#searchDate').val();

    dailyReportLoad(end, (err, res) => {
        if (!err) {
            dailyReportOptions.data = res.data;
            dailyReportOptions.columns = res.columns;
            dailyReportOptions.nestedHeaders = res.nestedHeaders;

            if(dailyReportTable){
                dailyReportTable.removeHook();
            }
            else{
                dailyReportTable = new Handsontable(document.querySelector('#kpi-daily-report'), dailyReportOptions);
            }
            dailyReportTable.updateSettings(dailyReportOptions);

            const detail_parent = $('#kpi-daily-issue');
            if(res.detailComments[0]) detail_parent.find('#detail-left').val(res.detailComments[0].context);
            if(res.detailComments[1]) detail_parent.find('#detail-right').val(res.detailComments[1].context);
        } else {
            console.log(err);
        }
    });
}

function dailyReportLoad(end, cb) {
    const begin = moment(end).subtract(1, 'month').format('YYYY-MM-01');
    const endDate = moment(end);

    $.ajax({
        url: `/business/kpi/daily/${begin}/${end}`,
        method: 'GET',
        dataType: 'json'
    }).done((data) => {
        if (data.success) {
            const res = {};
            const sum = {};
            const sum_gb = {};
            const sum_jp = {};
            const monthlyAcc = {};
            const headerKeys = [];

            const dateRows = data.adRevenues.reduce((acc, row) => {
                const app = row.app;
                const date = moment(row.date);
                const daily = date.locale('kr').format('YY/MM/DD(ddd)');
                const monthly = date.format('M월');
                
                if (!sum[daily]) {
                    sum[daily] = 0;
                    sum_gb[daily] = 0;
                    sum_jp[daily] = 0;
                }
                if (!sum[monthly]) {
                    sum[monthly] = 0;
                    sum_gb[monthly] = 0;
                    sum_jp[monthly] = 0;
                }
                sum[daily] += row.krw;
                sum[monthly] += row.krw;
                if (app.endsWith('_JP')) {
                    sum_jp[daily] += row.krw;
                    sum_jp[monthly] += row.krw;
                } else {
                    sum_gb[daily] += row.krw;
                    sum_gb[monthly] += row.krw;
                }

                if (!monthlyAcc[app]) {
                    acc[app] = {app};
                    monthlyAcc[app] = {};
                }
                if (!monthlyAcc[app][monthly]) {
                    monthlyAcc[app][monthly] = 0;
                    if (!headerKeys.includes(monthly))
                        headerKeys.push(monthly);
                }
                monthlyAcc[app][monthly] += row.krw;

                if (endDate.diff(date, 'days') < 6) {
                    if (!acc[app]) {
                        acc[app] = {app};
                    }
                    if (!acc[app][daily]) {
                        acc[app][daily] = 0;
                        if (!headerKeys.includes(daily))
                            headerKeys.push(daily);
                    }
                    acc[app][daily] += row.krw;
                }

                acc[app][monthly] = monthlyAcc[app][monthly];
                return acc;
            }, {});

            data.revenues.forEach(row => {
                row.revenue = Math.round(row.revenue);
                const app = row.app;
                const date = moment(row.date);
                const daily = date.locale('kr').format('YY/MM/DD(ddd)');
                const monthly = date.format('M월');
                
                if (!sum[daily]) {
                    sum[daily] = 0;
                    sum_gb[daily] = 0;
                    sum_jp[daily] = 0;
                }
                if (!sum[monthly]) {
                    sum[monthly] = 0;
                    sum_gb[monthly] = 0;
                    sum_jp[monthly] = 0;
                }
                sum[daily] += row.revenue;
                sum[monthly] += row.revenue;
                if (app.endsWith('_JP')) {
                    sum_jp[daily] += row.revenue;
                    sum_jp[monthly] += row.revenue;
                } else {
                    sum_gb[daily] += row.revenue;
                    sum_gb[monthly] += row.revenue;
                }
                
                if (!monthlyAcc[app]) {
                    dateRows[app] = {app};
                    monthlyAcc[app] = {};
                }
                if (!monthlyAcc[app][monthly]) {
                    monthlyAcc[app][monthly] = 0;
                    if (!headerKeys.includes(monthly))
                        headerKeys.push(monthly);
                }
                monthlyAcc[app][monthly] += row.revenue;

                if (endDate.diff(date, 'day') < 6) {
                    if (!dateRows[app]) {
                        dateRows[app] = {app};
                    }
                    if (!dateRows[app][daily]) {
                        dateRows[app][daily] = 0;
                        if (!headerKeys.includes(daily))
                            headerKeys.push(daily);
                    }
                    dateRows[app][daily] += row.revenue;
                }

                dateRows[app][monthly] = monthlyAcc[app][monthly];
            });

            const nestedHeaders = ['항목'];

            const rows = Object.keys(dateRows).sort((a, b) => {
                // 길이 짧은 게 앞
                let diff = a.length - b.length;
                if (diff) return diff;

                const [aName] = a.split('_');
                const [bName] = b.split('_');
                
                // 프로젝트 오더 순서로 정렬
                if (projectOrder.includes(aName) && projectOrder.includes(bName)) {
                    diff = projectOrder.indexOf(bName) - projectOrder.indexOf(aName);
                    if (diff) return diff;
                }

                // 글자 오름차순
                if (a > b) return -1;
                if (a < b) return 1;
                
                return 0;
            }).map(key => dateRows[key]);

            res.data = [
                // 이슈
                Object.keys(rows[0]).reduce((acc, key) => {
                    acc[key] = (key === 'app') ? '이슈' : '';
                    return acc;
                }, {}),

                // 프로젝트 별 데이터
                ...rows,

                // 합계
                Object.keys(rows[0]).reduce((acc, key) => {
                    acc[key] = (key === 'app') ? '합계' : sum[key];
                    return acc;
                }, {}),

                // 글로벌 합계
                Object.keys(rows[0]).reduce((acc, key) => {
                    acc[key] = (key === 'app') ? 'SS_GB' : sum_gb[key];
                    return acc;
                }, {}),

                // 일본 합계
                Object.keys(rows[0]).reduce((acc, key) => {
                    acc[key] = (key === 'app') ? 'SS_JP' : sum_jp[key];
                    return acc;
                }, {}),
            ];

            Object.keys(res.data[0]).forEach(key => {
                var mid_key = key.split('(')[0];
                mid_key = '20' + mid_key.replaceAll('/', '-');
                data.keyComments.forEach(comment => {
                    if(comment['date'] === mid_key){
                        res.data[0][key] = comment['issue'];
                    }
                });
            });

            res.columns = [{
                data: 'app',
                type: 'text'
            }, ...headerKeys.sort((a, b) => {
                // 길이 짧은 게 앞
                if (a.length === b.length) {
                    const aDate = moment(a, a.length < 4 ? 'M월' : 'YY/MM/DD(ddd)');
                    const bDate = moment(b, b.length < 4 ? 'M월' : 'YY/MM/DD(ddd)');
                    const diff = bDate.diff(aDate);
                    if (diff) return diff;
                }

                return a.length - b.length;
            }).map(key => {
                nestedHeaders.push(key);
                return {
                    data: key,
                    type: 'numeric',
                    numericFormat: {
                        pattern: {
                            mantissa: 0,
                            thousandSeparated: true
                        }
                    }
                };
            })];

            res.nestedHeaders = [nestedHeaders];

            res.detailComments = data.detailComments;
            cb(null, res);
        } else {
            console.log(data);
            cb(new Error('Unknown data type'));
        }
    }).fail((xhr, status, errorThrown) => {
        cb(new Error(errorThrown));
    });
}

//금일 이슈 저장
function insertDailyIssue(detail_left, detail_right){
    const today = moment().format("YYYY-MM-DD");

    $.ajax({
        url: `/business/kpi/dailyreports/comment/detail`,
        method: 'POST',
        dataType: 'json',
        data: { detail_left, detail_right, today }
    }).done((data) => {
        if (data.success) {
            // 성공
            dailyReportTableLoad();
        } else {
            console.log(data);
        }
    }).fail((xhr, status, errorThrown) => {
        if (xhr.readyState === 4) {
        } else { }
    });
}