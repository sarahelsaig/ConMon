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
        .then(function (x) {
            if (x !== "granted") throw message;
        })
        .then(_ => new Notification(message))
        .catch(alert);
}

function notifyError(e) {
    notify(e.Message ? e.Message : e);
    console.log(e);
}

const notAnExecption = function (r) {
    if (r !== true) throw r;
    return true;
};

// source: https://stackoverflow.com/a/40341495
let elementIsVisibleFunction = null;
(function () {
    function elementIsVisible(element, container, partial) {
        if (!element || !container) return false;
        const contHeight = container.offsetHeight,
            elemTop = offset(element).top - offset(container).top,
            elemBottom = elemTop + element.offsetHeight;
        return (elemTop >= 0 && elemBottom <= contHeight) ||
            (partial && ((elemTop < 0 && elemBottom > 0) || (elemTop > 0 && elemTop <= contHeight)))
    }

    // checks window
    function isWindow(obj) {
        return obj != null && obj === obj.window;
    }

    // returns corresponding window
    function getWindow(elem) {
        return isWindow(elem) ? elem : elem.nodeType === 9 && elem.defaultView;
    }

    // taken from jquery
    // @returns {{top: number, left: number}} 
    function offset(elem) {
        let docElem, win,
            box = {top: 0, left: 0},
            doc = elem && elem.ownerDocument;

        docElem = doc.documentElement;

        if (typeof elem.getBoundingClientRect !== typeof undefined) {
            box = elem.getBoundingClientRect();
        }
        win = getWindow(doc);
        return {
            top: box.top + win.pageYOffset - docElem.clientTop,
            left: box.left + win.pageXOffset - docElem.clientLeft
        };
    }
    
    elementIsVisibleFunction = elementIsVisible;
})();

export default {
    postData: postData,
    getData: getData,

    notify: notify,
    notifyError: notifyError,
    notAnExecption: notAnExecption,

    elementIsVisible: elementIsVisibleFunction
};