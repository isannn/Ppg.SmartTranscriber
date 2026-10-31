// Media-transcript sync: highlights the segment row under playhead and
// seeks the player when a row is clicked.
window.stMedia = {
    bind: function (el) {
        if (!el || el._stBound) {
            return;
        }
        el._stBound = true;
        el.addEventListener('timeupdate', function () {
            var t = el.currentTime;
            document.querySelectorAll('tr[data-start]').forEach(function (r) {
                var s = parseFloat(r.dataset.start);
                var e = parseFloat(r.dataset.end);
                r.classList.toggle('table-active', t >= s && t < e);
            });
        });
    },
    seek: function (el, seconds) {
        if (!el) {
            return;
        }
        el.currentTime = seconds;
        el.play();
    }
};
