function postData(url, data = {}, mode = 'same-origin') {
    // Default options are marked with *
    return fetch(url, {
        method: "POST", // *GET, POST, PUT, DELETE, etc.
        mode: mode, // no-cors, cors, *same-origin
        cache: "no-cache", // *default, no-cache, reload, force-cache, only-if-cached
        credentials: "same-origin", // include, *same-origin, omit
        headers: {
            "Content-Type": "application/json",
            // "Content-Type": "application/x-www-form-urlencoded",
        },
        redirect: "follow", // manual, *follow, error
        referrer: "no-referrer", // no-referrer, *client
        body: JSON.stringify(data), // body data type must match "Content-Type" header
    })
        .then(response => response.json()); // parses JSON response into native Javascript objects 
}
const getData = url => fetch(url).then(response => response.json());

function notify(message) {
    return Notification.requestPermission()
        .then(function (x) { if (x !== "granted") throw message; })
        .then(_ => new Notification(message))
        .catch(alert);
}
function notifyError(e) { notify(e.Message ? e.Message : e); console.log(e); }
const notAnExecption = function (r) { if (r !== true) throw r; return true; };

const app = new Vue({
    el: '#app',
    data: {
        apphosts: [],
        selected: null,
        addModel: null,
        addModelMode: 'single',
        addModelBatch: '',
        lines: {},
        lastLine: {},
        running: {}
    },
    methods: {
        periodicCheck: function () {
            const self = this;
            for (let i = 0; i < self.apphosts.length; i++) {
                const label = self.apphosts[i];
                const lastLine = label in self.lastLine ? self.lastLine[label] : 0;
                getData(`/api/schedule/lines?label=${label}&after=${lastLine}`)
                    .then(function (r) {
                        self.lastLine[label] = r.lastId;
                        self.lines[label] = label in self.lines ? self.lines[label].concat(r.lines) : r.lines;
                        getData(`/api/schedule/running?label=${label}`)
                            .then(r => self.running[label] = r)
                            .then(_ => self.$forceUpdate());
                    })
                    .catch(notifyError);
            }
        },
        add: function () {
            const self = this;
            const isSingle = self.addModelMode === 'single';

            let url = isSingle ? '/api/schedule/add' : '/api/schedule/addmany';
            let model = self.addModel;

            if (isSingle) {
                if (self.addModel.Label.trim() === '') {
                    document.getElementById('add-model-label').focus();
                    return;
                }
            }
            else {
                try { model = JSON.parse(self.addModelBatch); }
                catch (e) { notify("Couldn't parse JSON!"); return; }
            }

            postData(url, model)
                .then(notAnExecption)
                .then(function () { self.addModel = null; self.periodicCheck(); })
                .catch(console.log);

            self.addModel = "Sending...";
        },
        trigger: function () {
            const self = this;
            const label = self.selected;
            getData(`/api/schedule/trigger?label=${label}`)
                .then(notAnExecption)
                .then(x => self.periodicCheck())
                .catch(notifyError);
        },
        erase: function () {
            const self = this;
            const label = self.selected;
            getData(`/api/schedule/erase?label=${label}`)
                .then(notAnExecption)
                .then(function () { self.lines[label] = []; self.$forceUpdate(); })
                .catch(notifyError);
        },
        navigateTo: url => location.href = url,
        cronstrue: function (str) {
            try {
                return cronstrue.toString(str);
            }
            catch (e) {
                return '???';
            }
        },
    },
    mounted: async function () {
        const self = this;
        self.apphosts = await getData('/api/schedule/apps');

        self.periodicCheck();
        setInterval(function () { self.periodicCheck(); }, 10000);
    }
});