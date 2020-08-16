<template>
    <div class="index-app">
        <header>
            <ul class="menu">
                <li>
                    <button @click="displayAdd">Add</button>
                </li>
                <li>
                    <button :disabled="selected === null || running[selected]" @click="trigger">Launch</button>
                </li>
                <li>
                    <button :disabled="selected === null" @click="erase">Erase Lines</button>
                </li>
                <li>
                    <button :disabled="selected === null"
                            @click="mode = (mode === 'consoleOutput' ? 'logFiles' : 'consoleOutput')">
                        {{ mode === 'consoleOutput' ? 'Download Logs' : 'See Console Output' }}
                    </button>
                </li>
                <li>
                    <button @click="navigateTo('/hangfire')">Dashboard</button>
                </li>
                <li class="space">&nbsp;</li>
                <li class="title"><h1>ConMon!</h1></li>
            </ul>
            <div v-if="addModel !== null" class="add-model">
                <div v-if="typeof addModel === 'object'">
                    <div>
                        <label><input type="radio" value="single" v-model="addModelMode"/>Single</label>
                        <label><input type="radio" value="batch" v-model="addModelMode"/>Batch</label>
                    </div>
                    <template v-if="addModelMode === 'single'">
                        <div>
                            <label for="add-model-label">Label</label>
                            <input id="add-model-label" v-model="addModel.Label"/>
                        </div>
                        <div>
                            <label for="add-model-program">Program</label>
                            <input id="add-model-program" v-model="addModel.Program"/>
                        </div>
                        <div>
                            <label for="add-model-arguments">Arguments</label>
                            <input id="add-model-arguments" v-model="addModel.Arguments"/>
                        </div>
                        <div>
                            <label for="add-model-directory">Working Directory</label>
                            <input id="add-model-directory" v-model="addModel.WorkingDirectory"/>
                        </div>
                        <div>
                            <label for="add-model-cron">Cron Expression</label>
                            <input id="add-model-cron" v-model="addModel.Cron"/>
                            <div><i>{{ cronstrue(addModel.Cron) }}</i></div>
                            <button class="wide" @click="showCronEditor = true">Show Editor</button>
                        </div>
                    </template>
                    <template v-else>
                        <label for="add-multiple">JSON</label>
                        <textarea id="add-multiple" v-model="addModelBatch"></textarea>
                    </template>
                    <div class="add-model-buttons">
                        <button @click="add">Ok</button>
                        <button @click="addModel = null">Cancel</button>
                    </div>
                </div>
                <div v-else-if="typeof addModel === 'string'">{{ addModel }}</div>
            </div>
            <div v-if="addModel !== null && showCronEditor" class="add-model">
                <div>
                    <cron-editor v-model="addModel.Cron"/>
                    <button class="wide" @click="showCronEditor = false">Done</button>
                </div>
            </div>
        </header>
        <nav>
            <ul class="hosts">
                <li v-for="h in appHosts"
                    :class="{ host: true, 'host-running' : running[h], 'host-selected' : selected === h }"
                    @click="selected = h">
                    {{ running[h] ? '👷' : '⏱️' }} <span>{{ h }}</span>
                </li>
            </ul>
        </nav>
        <main class="console-output" v-if="mode === 'consoleOutput'">
            <ul v-if="selected !== null">
                <li v-for="(line, i) in lines[selected]">{{ i.toString().padStart(5, '\xa0') }}: {{ line }}</li>
            </ul>
        </main>
        <main class="log-files" v-else-if="mode === 'logFiles'">
            <h1 v-if="currentLogs === null">Loading...</h1>
            <template v-else>
                <h1>Log and Config Files</h1>
                <section v-for="x in currentLogs">
                    <h2>{{ x.Key.toUpperCase() }}</h2>
                    <ul>
                        <li v-for="log in x.Value">
                            <a :href="`/api/schedule/logFile?label=${e(selected)}&file=${e(log)}`">{{ log }}</a>
                        </li>
                    </ul>
                </section>
            </template>
        </main>
        <main v-else><h2>Corrupted mode stat: '{{ mode }}'!</h2></main>
        <footer>
        </footer>
    </div>
</template>

<script>
    import utils from './utils.js';
    import VueCronEditorBuefy from 'vue-cron-editor-buefy';
    import cronstrue from 'cronstrue';

    window.utils = utils;

    export default {
        name: 'index-app',
        data() {
            return {
                mode: 'consoleOutput',
                appHosts: [],
                selected: null,
                addModel: null,
                addModelMode: 'single',
                addModelBatch: '',
                lines: {},
                lastLine: {},
                running: {},
                showCronEditor: false,
                currentLogs: null,
            };
        },
        methods: {
            e: x => encodeURIComponent(x),
            periodicLineCheck: function () {
                const self = this;
                const loadingText = 'Loading...';

                const label = self.selected;
                if (!label) return;

                if (!self.lines[label] || !self.lines[label].length) self.lines[label] = [];
                if (self.lines[label][self.lines[label].length - 1] === loadingText) return;

                const lastLine = label in self.lastLine ? self.lastLine[label] : 0;
                if (lastLine > 0 && !self.isLastLineVisible()) return;

                self.lines[label].push(loadingText);
                utils.getData(`/api/schedule/lines?label=${label}&after=${lastLine}`)
                    .then(function (r) {
                        self.lines[label].pop();
                        self.lastLine[label] = r.lastId;
                        self.lines[label] = label in self.lines ? self.lines[label].concat(r.lines) : r.lines;
                    })
                    .then(_ => self.$forceUpdate())
                    .catch(utils.notifyError);
            },
            periodicActivityCheck: function () {
                const self = this;

                self.appHosts.forEach(label =>
                    utils.getData(`/api/schedule/running?label=${label}`)
                        .then(r => self.running[label] = r)
                        .then(_ => self.$forceUpdate())
                        .catch(utils.notifyError));
            },
            displayAdd: function () {
                this.showCronEditor = false;
                this.addModel = {
                    Program: '',
                    Arguments: '',
                    WorkingDirectory: '',
                    Label: '',
                    Cron: '0 2 * * *'
                };
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
                } else {
                    try {
                        model = JSON.parse(self.addModelBatch);
                    } catch (e) {
                        utils.notify("Couldn't parse JSON!");
                        return;
                    }
                }

                utils.postData(url, model)
                    .then(utils.notAnException)
                    .then(function () {
                        self.addModel = null;
                        self.periodicActivityCheck();
                    })
                    .catch(console.log);

                self.addModel = "Sending...";
            },
            trigger: function () {
                const self = this;
                const label = self.selected;
                utils.getData(`/api/schedule/trigger?label=${label}`)
                    .then(utils.notAnException)
                    .then(_ => self.periodicActivityCheck())
                    .catch(utils.notifyError);
            },
            erase: function () {
                const self = this;
                const label = self.selected;
                utils.getData(`/api/schedule/erase?label=${label}`)
                    .then(utils.notAnException)
                    .then(function () {
                        self.lines[label] = [];
                        self.$forceUpdate();
                    })
                    .catch(utils.notifyError);
            },
            navigateTo: url => location.href = url,
            cronstrue: function (str) {
                try {
                    return cronstrue.toString(str);
                } catch (e) {
                    return '???';
                }
            },
            isLastLineVisible: function () {
                const container = '.index-app .console-output';
                const lastChild = container + ' ul li:last-child';
                return utils.elementIsVisible(
                    document.querySelector(lastChild),
                    document.querySelector(container),
                    false);
            },
            updateLogs: function() {
                const self = this;

                self.currentLogs = null;
                utils.getData('/api/schedule/logFiles?label=' + self.selected)
                    .then(r => self.currentLogs = r)
                    .catch(utils.notifyError);
            },
        },
        watch: {
            selected: function () {
                this.periodicActivityCheck();
                if (this.mode === 'logFiles') this.updateLogs();
            },
            mode: function () {
                if (this.mode === 'logFiles') this.updateLogs();
            },
        },
        components: {'cron-editor': VueCronEditorBuefy},
        mounted: async function () {
            const self = this;
            window.app = this;
            self.appHosts = await utils.getData('/api/schedule/apps');

            self.periodicLineCheck();
            setInterval(function () {
                self.periodicLineCheck();
            }, 100);

            self.periodicActivityCheck();
            setInterval(function () {
                self.periodicActivityCheck();
            }, 30000);
        }
    };
</script>

<style lang="scss">
    @import "../Styles/index-app";
</style>