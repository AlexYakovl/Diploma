// Прямая отправка multipart из браузера на API (минуя SignalR Blazor Server).
(function () {
    const photosite = (window.photosite = window.photosite || {});

    photosite.uploadMultipartFieldsOnly = function (method, url, token, fieldsJson) {
        return new Promise(function (resolve) {
            try {
                var fd = new FormData();
                var pairs = fieldsJson ? JSON.parse(fieldsJson) : [];
                for (var i = 0; i < pairs.length; i++) {
                    var p = pairs[i];
                    fd.append(p.name, p.value == null ? "" : String(p.value));
                }
                var xhr = new XMLHttpRequest();
                xhr.open((method || "POST").toUpperCase(), url, true);
                if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);
                xhr.onreadystatechange = function () {
                    if (xhr.readyState !== 4) return;
                    resolve({
                        ok: xhr.status >= 200 && xhr.status < 300,
                        status: xhr.status,
                        body: xhr.responseText || "",
                    });
                };
                xhr.onerror = function () {
                    resolve({
                        ok: false,
                        status: xhr.status || 0,
                        body: xhr.responseText || "network error",
                    });
                };
                xhr.send(fd);
            } catch (err) {
                resolve({
                    ok: false,
                    status: 0,
                    body: String(err && err.message ? err.message : err),
                });
            }
        });
    };

    photosite.uploadMultipartForm = function (
        method,
        url,
        token,
        fieldsJson,
        fileInputEl,
        fileFieldName,
        dotNetRef,
        progressMethodName
    ) {
        return new Promise(function (resolve) {
            try {
                var fd = new FormData();
                var pairs = fieldsJson ? JSON.parse(fieldsJson) : [];
                for (var i = 0; i < pairs.length; i++) {
                    var p = pairs[i];
                    fd.append(p.name, p.value == null ? "" : String(p.value));
                }
                if (fileInputEl && fileFieldName) {
                    var file = fileInputEl.files && fileInputEl.files[0];
                    if (file) fd.append(fileFieldName, file, file.name);
                }

                var xhr = new XMLHttpRequest();
                xhr.open((method || "POST").toUpperCase(), url, true);
                if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);
                xhr.upload.onprogress = function (e) {
                    if (!dotNetRef || !progressMethodName) return;
                    var loaded = e.loaded || 0;
                    var total = e.lengthComputable ? e.total : 0;
                    dotNetRef.invokeMethodAsync(progressMethodName, loaded, total);
                };
                xhr.onreadystatechange = function () {
                    if (xhr.readyState !== 4) return;
                    resolve({
                        ok: xhr.status >= 200 && xhr.status < 300,
                        status: xhr.status,
                        body: xhr.responseText || "",
                    });
                };
                xhr.onerror = function () {
                    resolve({
                        ok: false,
                        status: xhr.status || 0,
                        body: xhr.responseText || "network error",
                    });
                };
                xhr.send(fd);
            } catch (err) {
                resolve({
                    ok: false,
                    status: 0,
                    body: String(err && err.message ? err.message : err),
                });
            }
        });
    };

    /// <summary>Загрузка фото в альбом: по одному файлу за POST (очередь), с xhr.upload progress.</summary>
    photosite.uploadAlbumPhotosSequential = function (url, token, inputEl, dotNetRef, progressMethodName) {
        return new Promise(function (resolve) {
            try {
                var files = Array.from((inputEl && inputEl.files) || []);
                if (!files.length) {
                    resolve({
                        ok: false,
                        uploaded: 0,
                        total: 0,
                        status: 0,
                        body: "no files",
                    });
                    return;
                }

                var fileCount = files.length;
                var uploaded = 0;
                var lastStatus = 0;
                var lastBody = "";

                function notifyProgress(loaded, total, index, name) {
                    if (!dotNetRef || !progressMethodName) return;
                    dotNetRef.invokeMethodAsync(
                        progressMethodName,
                        loaded,
                        total,
                        index,
                        fileCount,
                        name || ""
                    );
                }

                function uploadNext(index) {
                    if (index >= fileCount) {
                        resolve({
                            ok: uploaded === fileCount,
                            uploaded: uploaded,
                            total: fileCount,
                            status: lastStatus,
                            body: lastBody || "",
                        });
                        return;
                    }

                    var file = files[index];
                    notifyProgress(0, 1, index, file.name);

                    var fd = new FormData();
                    fd.append("files", file, file.name);

                    var xhr = new XMLHttpRequest();
                    xhr.open("POST", url, true);
                    if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);

                    xhr.upload.onprogress = function (e) {
                        var loaded = e.loaded || 0;
                        var total = e.lengthComputable ? e.total : 0;
                        notifyProgress(loaded, total, index, file.name);
                    };

                    xhr.onreadystatechange = function () {
                        if (xhr.readyState !== 4) return;
                        lastStatus = xhr.status;
                        lastBody = xhr.responseText || "";
                        if (xhr.status >= 200 && xhr.status < 300) {
                            uploaded++;
                            notifyProgress(1, 1, index, file.name);
                            uploadNext(index + 1);
                        } else {
                            resolve({
                                ok: false,
                                uploaded: uploaded,
                                total: fileCount,
                                status: lastStatus,
                                body: lastBody,
                            });
                        }
                    };

                    xhr.onerror = function () {
                        resolve({
                            ok: false,
                            uploaded: uploaded,
                            total: fileCount,
                            status: xhr.status || 0,
                            body: xhr.responseText || "network error",
                        });
                    };

                    xhr.send(fd);
                }

                uploadNext(0);
            } catch (err) {
                resolve({
                    ok: false,
                    uploaded: 0,
                    total: 0,
                    status: 0,
                    body: String(err && err.message ? err.message : err),
                });
            }
        });
    };

    photosite.uploadAboutSliderBatch = function (url, token, inputEl, dotNetRef, progressMethodName) {
        return new Promise(function (resolve) {
            try {
                var files = Array.from((inputEl && inputEl.files) || []);
                if (!files.length) {
                    resolve({
                        ok: false,
                        uploaded: 0,
                        total: 0,
                        status: 0,
                        body: "no files",
                    });
                    return;
                }

                var fileCount = files.length;
                var uploaded = 0;
                var lastStatus = 0;
                var lastBody = "";

                function notifyProgress(loaded, total, index, name) {
                    if (!dotNetRef || !progressMethodName) return;
                    dotNetRef.invokeMethodAsync(
                        progressMethodName,
                        loaded,
                        total,
                        index,
                        fileCount,
                        name || ""
                    );
                }

                function uploadNext(index) {
                    if (index >= fileCount) {
                        resolve({
                            ok: uploaded === fileCount,
                            uploaded: uploaded,
                            total: fileCount,
                            status: lastStatus,
                            body: lastBody || "",
                        });
                        return;
                    }

                    var file = files[index];
                    notifyProgress(0, 1, index, file.name);

                    var fd = new FormData();
                    fd.append("file", file, file.name);

                    var xhr = new XMLHttpRequest();
                    xhr.open("POST", url, true);
                    if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);

                    xhr.upload.onprogress = function (e) {
                        var loaded = e.loaded || 0;
                        var total = e.lengthComputable ? e.total : 0;
                        notifyProgress(loaded, total, index, file.name);
                    };

                    xhr.onreadystatechange = function () {
                        if (xhr.readyState !== 4) return;
                        lastStatus = xhr.status;
                        lastBody = xhr.responseText || "";
                        if (xhr.status >= 200 && xhr.status < 300) {
                            uploaded++;
                            notifyProgress(1, 1, index, file.name);
                            uploadNext(index + 1);
                        } else {
                            resolve({
                                ok: false,
                                uploaded: uploaded,
                                total: fileCount,
                                status: lastStatus,
                                body: lastBody,
                            });
                        }
                    };

                    xhr.onerror = function () {
                        resolve({
                            ok: false,
                            uploaded: uploaded,
                            total: fileCount,
                            status: xhr.status || 0,
                            body: xhr.responseText || "network error",
                        });
                    };

                    xhr.send(fd);
                }

                uploadNext(0);
            } catch (err) {
                resolve({
                    ok: false,
                    uploaded: 0,
                    total: 0,
                    status: 0,
                    body: String(err && err.message ? err.message : err),
                });
            }
        });
    };

    photosite.inputFirstFileName = function (inputEl) {
        try {
            var f = inputEl && inputEl.files && inputEl.files[0];
            return f ? f.name : "";
        } catch (_) {
            return "";
        }
    };

    photosite.clearFileInput = function (el) {
        try {
            if (el) el.value = "";
        } catch (_) {}
    };
})();
