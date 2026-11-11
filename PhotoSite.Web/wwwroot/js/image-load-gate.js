// Ждём загрузку изображений в контейнере (для плавного показа публичных страниц).
(function () {
    var photosite = (window.photosite = window.photosite || {});

    // Учитывает img с src; при skipLazy не ждём loading="lazy" (ниже сгиба догрузятся после показа страницы).
    photosite.waitForMainImages = function (rootSelector, timeoutMs, skipLazy) {
        return new Promise(function (resolve) {
            try {
                var root = rootSelector ? document.querySelector(rootSelector) : document.body;
                if (!root) {
                    resolve();
                    return;
                }

                var sel = skipLazy !== false ? 'img[src]:not([loading="lazy"])' : 'img[src]';
                var imgs = Array.prototype.slice.call(root.querySelectorAll(sel)).filter(function (img) {
                    var s = img.getAttribute('src') || '';
                    return s.length > 0 && s.indexOf('data:') !== 0;
                });

                var pending = imgs.filter(function (img) {
                    return !img.complete;
                });

                if (pending.length === 0) {
                    requestAnimationFrame(function () {
                        resolve();
                    });
                    return;
                }

                var finished = false;
                function done() {
                    if (finished) return;
                    finished = true;
                    clearTimeout(timer);
                    resolve();
                }

                var ms = typeof timeoutMs === 'number' && timeoutMs > 0 ? timeoutMs : 12000;
                var timer = setTimeout(done, ms);

                var left = pending.length;
                function one() {
                    left--;
                    if (left <= 0) done();
                }

                pending.forEach(function (img) {
                    img.addEventListener('load', one, { once: true });
                    img.addEventListener('error', one, { once: true });
                });
            } catch (_) {
                resolve();
            }
        });
    };
})();
