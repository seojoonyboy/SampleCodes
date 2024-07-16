let analyticsTable = null;
const analyticsOptions = {
    data: [],
    columns: [
        { data: 'date', type: 'text', },
        { data: 'update', type: 'text', },
        { data: 'issue', type: 'text', },
        {
            data: (row) => row.installs_Android + row.installs_iOS,
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: 'installs_Android',
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: 'installs_iOS',
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: 'NRU',
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: 'DAU',
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: 'PU',
            type: 'numeric',
            numericFormat: { pattern: { thousandSeparated: true }, },
        },
        {
            data: (row) => row.PU / row.DAU,    //PUR
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                },
            },
        },
        {
            data: (row) => (row.revenue_Android + row.revenue_iOS + row.AD) / row.DAU,    //ARPU
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
        {
            data: (row) => (row.revenue_Android + row.revenue_iOS) / row.PU,   //ARPPU
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
        {
            data: (row) => row.revenue_Android + row.revenue_iOS + row.AD,   //종합매출
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
        {
            data: (row) => row.revenue_Android + row.revenue_iOS,       //인앱매출
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
        {
            data: 'AD',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true     //광고매출
                },
            },
        },
        {
            data: (row) => row.AD / (row.revenue_Android + row.revenue_iOS + row.AD),
            type: 'numeric',
            numericFormat: {
                pattern: {
                    output: 'percent',
                    mantissa: 2,
                    trimMantissa: true,
                    spaceSeparated: true,
                },
            },
        },
        {
            data: 'revenue_Android',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
        {
            data: 'revenue_iOS',
            type: 'numeric',
            numericFormat: {
                pattern: {
                    mantissa: 0,
                    thousandSeparated: true
                },
            },
        },
    ],
    width: '100%',
    height: '100%',
    autoRowSize: true,
    manualRowResize: true,
    manualColumnResize: true,
    nestedHeaders: [[
        '일자/항목', '업데이트', '이슈', '설치합계', 'AOS-설치', 'IOS-설치',
        'NRU', 'DAU', 'PU', 'PUR', 'ARPU', 'ARPPU',
        '종합매출', '인앱매출', '광고매출', '광고비율', 'AOS매출', 'IOS매출'
    ]],
    fixedColumnsLeft: 1,
    licenseKey: 'non-commercial-and-evaluation',
    cells: (row, col) => {
        return {
            wordWrap: [1, 2].includes(col),
            renderer (instance, td, row, col, prop, value, cellProperties) {
                Handsontable.renderers.NumericRenderer.apply(this, arguments);
                if ([3, 9, 10, 11, 12, 15].includes(col)) {
                    td.style.background = 'rgb(226,239,218)';
                }
            }
        };
    },
    afterChange: (list, action) => {
        if (action === 'edit') {
            const app = $('#selectProjectButton').attr('data-value');
            const begin = moment($('#beginDate').val());
            list.forEach(data => {
                const date = moment(begin).add(data[0], 'days').format('YYYY-MM-DD');
                const column = data[1];
                const comment = data[3];
                
                $.ajax({
                    url: `/business/kpi/analytics/comment/${app}/${column}/${date}`,
                    method: 'POST',
                    dataType: 'json',
                    data: { comment }
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
            });
        }
    }
};

function analyticsTableLoad() {
    const app = $('#selectProjectButton').attr('data-value');
    const begin = $('#beginDate').val();
    const end = $('#endDate').val();

    analyticsLoad(app, begin, end, (err, data) => {
        if (!err) {
            analyticsOptions.data = Object.values(data);

            if(analyticsTable){
                analyticsTable.removeHook();
            }
            else{
                analyticsTable = new Handsontable(document.querySelector('#kpi-analytics'), analyticsOptions);
            }
            analyticsTable.updateSettings(analyticsOptions);
        } else {
            console.log(err);
        }
    });
}

function analyticsLoad(app, begin, end, cb) {
    $.ajax({
        url: `/business/kpi/analytics/${app}/${begin}/${end}`,
        method: 'GET',
        dataType: 'json'
    }).done((data) => {
        if (data.success && data.analytics) {
            const dateRows = data.analytics.sort((a, b) => {
                return new Date(a.date).getTime() - new Date(b.date).getTime();
            }).reduce((acc, row) => {
                const date = row.date.split('T')[0];
                if (!acc[date]) {
                    acc[date] = {date};
                }

                //JP의 경우 Analytics에 엔화 기준으로 입력되어 있기 때문에 한화로 (강제로)변경해줄 필요가 있음
                //JP의 AD값은 KRW로 환전이 이미 된 상태이다.
                //TODO : 근본적으로 수정
                if(row.app.includes('JP') && row.JPY !== undefined) {
                    row.revenue_Android *= row.JPY;
                    row.revenue_iOS *= row.JPY;
                }

                Object.keys(row).forEach(key => {
                    if (key === 'date')
                        return;
                    const value = row[key];
                    if (typeof value !== 'number') {
                        acc[date][key] = value;
                        return;
                    }

                    if (!acc[date][key]) {
                        acc[date][key] = 0;
                    }
                    acc[date][key] += value;
                });
                
                return acc;
            }, {});

            data.comments.forEach(row => {
                const date = row.date.split('T')[0];
                if (row.update) {
                    dateRows[date].update = row.update;
                }
                if (row.issue) {
                    dateRows[date].issue = row.issue;
                }
            });

            cb(null, dateRows);
        } else {
            console.log(data);
            cb(new Error('Unknown data type'));
        }
    }).fail((xhr, status, errorThrown) => {
        cb(new Error(errorThrown));
    });
}
