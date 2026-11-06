mergeInto(LibraryManager.library, {
  NovaCrumbSet: function (s) { try { localStorage.setItem("novaCrumb", UTF8ToString(s)); } catch (e) {} },
  NovaCrumbGet: function () {
    var v = ""; try { v = localStorage.getItem("novaCrumb") || ""; } catch (e) {}
    var n = lengthBytesUTF8(v) + 1, b = _malloc(n); stringToUTF8(v, b, n); return b;
  }
});
