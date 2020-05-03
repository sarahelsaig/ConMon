import fromEntries from "object.fromentries";
import IndexApp from "./index-app.vue";
import '../Styles/site.scss';


if (!Object.fromEntries) { fromEntries.shim(); }
const useComponents = window.useComponents = (...components) => Object.fromEntries(components.map(x => [x.name, x]));
const Vue = window.Vue;

window.app = window.app = new Vue({
    el: '#app',
    template: '<index-app/>',
    components: useComponents(IndexApp)
});