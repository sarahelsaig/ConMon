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


export default {
    postData: postData,
    getData: getData,

    notify: notify,
    notifyError: notifyError,
    notAnExecption: notAnExecption
};