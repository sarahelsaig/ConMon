import IndexApp from "./index-app.vue";
import fromEntries from "object.fromentries";


if (!Object.fromEntries) { fromEntries.shim(); }
const useComponents = window.useComponents = (...components) => Object.fromEntries(components.map(x => [x.name, x]));


const app = window.app = new Vue({
    el: '#app',
    template: '<index-app/>',
    components: useComponents(IndexApp)
});