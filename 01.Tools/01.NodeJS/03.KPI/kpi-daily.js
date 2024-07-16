let dailyTable = null;

const dailyOptions = {
    data: [],
    columns: [],
    width: '100%',
    height: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    colHeaders: ['Date'],
    fixedColumnsLeft: 1,
    licenseKey: 'non-commercial-and-evaluation',
    cells: (row, col) => {
        return {
            wordWrap: false,
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if (prop.includes('_AD')) {
                    td.style.background = 'rgb(226,239,218)';
                }
            }
        };
    }
};

function dailyTableLoad() {
    const begin = $('#beginDate').val();
    const end = $('#endDate').val();

    dailyLoad(begin, end, (err, res) => {
        if (!err) {
            dailyOptions.data = res.data;
            dailyOptions.columns = res.columns;
            dailyOptions.colHeaders = res.colHeaders;

            if(dailyTable){
                dailyTable.removeHook();
            }
            else{
                dailyTable = new Handsontable(document.querySelector('#kpi-daily'), dailyOptions);
            }
            dailyTable.updateSettings(dailyOptions);
        } else {
            console.log(err);
        }
    });
}

function dailyLoad(begin, end, cb) {
    const beginFirstDay = moment(begin).format('YYYY-MM-01');   // 월별 누적을 위해 시작일을 1일로 잡는다

    $.ajax({
        url: `/business/kpi/daily/${beginFirstDay}/${end}`,
        method: 'GET',
        dataType: 'json'
    }).done((data) => {
        if (data.success) {
            const res = {};
            const monthlyAcc = {};
            const headerKeys = [];
            const dateRows = data.adRevenues.sort((a, b) => {
                return new Date(a.date).getTime() - new Date(b.date).getTime();
            }).reduce((acc, row) => {
                if (!row.category)
                    return acc;

                const app = `${row.app}${row.category ? '_' : ''}${row.category}`;
                const date = row.date.split('T')[0];
                const monthly = date.split(/\-[0-9]+$/)[0];
                
                if (!monthlyAcc[monthly]) {
                    monthlyAcc[monthly] = {};
                }
                if (!monthlyAcc[monthly][app]) {
                    monthlyAcc[monthly][app] = 0;
                }
                monthlyAcc[monthly][app] += row.krw;

                if (moment(date) < moment(begin)) {
                    // 월별 누적 계산만 하면 되기 때문에 시작일 이전은 표시하지 않는다
                    return acc;
                }

                if (!acc[date]) {
                    acc[date] = {date};
                }
                if (!acc[date][app]) {
                    acc[date][app] = 0;
                    if (!headerKeys.includes(app))
                        headerKeys.push(app);
                    if (!headerKeys.includes(`${app}_ACC`))
                        headerKeys.push(`${app}_ACC`);
                }
                acc[date][app] += row.krw;
                acc[date][`${app}_ACC`] = monthlyAcc[monthly][app];
                return acc;
            }, {});

            data.revenues.forEach(row => {
                const date = row.date.split('T')[0];
                const monthly = date.split(/\-[0-9]+$/)[0];

                if (!monthlyAcc[monthly]) {
                    monthlyAcc[monthly] = {};
                }
                if (!monthlyAcc[monthly][row.app]) {
                    monthlyAcc[monthly][row.app] = 0;
                }
                monthlyAcc[monthly][row.app] += row.revenue;

                if (moment(date) < moment(begin)) {
                    // 월별 누적 계산만 하면 되기 때문에 시작일 이전은 표시하지 않는다
                    return;
                }

                if (!dateRows[date]) {
                    dateRows[date] = {date};
                }
                if (!dateRows[date][row.app]) {
                    dateRows[date][row.app] = 0;
                    if (!headerKeys.includes(row.app))
                        headerKeys.push(row.app);
                    if (!headerKeys.includes(`${row.app}_ACC`))
                        headerKeys.push(`${row.app}_ACC`);
                }
                dateRows[date][row.app] += row.revenue;
                dateRows[date][`${row.app}_ACC`] = monthlyAcc[monthly][row.app];
            });
            
            const colHeaders = ['Date'];
            res.data = Object.values(dateRows);
            res.columns = [{
                data: 'date',
                type: 'text',
            }, ...headerKeys.sort((a, b) => {
                // 월별 누적은 제일 마지막에
                if (a.match(/_ACC$/) && !b.match(/_ACC$/)) return 1;
                if (!a.match(/_ACC$/) && b.match(/_ACC$/)) return -1;

                // 길이 짧은 게 앞
                let diff = a.length - b.length;
                if (diff) return diff;

                // AD가 앞에..
                const [aName, aSuffix1, aSuffix2] = a.split('_');
                const [bName, bSuffix1, bSuffix2] = b.split('_');
                if (aSuffix1 === 'AD' && bSuffix1 !== 'AD') return -1;
                if (bSuffix1 === 'AD' && aSuffix1 !== 'AD') return 1;
                if (aSuffix2 === 'AD' && bSuffix2 !== 'AD') return -1;
                if (bSuffix2 === 'AD' && aSuffix2 !== 'AD') return 1;
                
                // 프로젝트 오더 순서로 정렬
                if (projectOrder.includes(aName) && projectOrder.includes(bName)) {
                    diff = projectOrder.indexOf(bName) - projectOrder.indexOf(aName);
                    if (diff) return diff;
                }
                
                // 글자 오름차순
                if (a > b) return -1;
                if (a < b) return 1;
                
                return 0;
            }).map(key => {
                colHeaders.push(key.replace('_ACC', ' 월별 누적'));
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

            res.colHeaders = colHeaders;

            cb(null, res);
        } else {
            console.log(data);
            cb(new Error('Unknown data type'));
        }
    }).fail((xhr, status, errorThrown) => {
        cb(new Error(errorThrown));
    });
}
