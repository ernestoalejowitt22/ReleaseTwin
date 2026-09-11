// local-evidence-viewer: progressive enhancement only. The report is complete and readable with
// scripting disabled — this just highlights the case you are looking at in the left-hand list.
(function () {
  "use strict";

  var links = Array.prototype.slice.call(document.querySelectorAll(".rt-nav a[href^='#']"));
  if (links.length === 0 || typeof IntersectionObserver !== "function") {
    return;
  }

  var byId = {};
  links.forEach(function (link) {
    byId[link.getAttribute("href").slice(1)] = link;
  });

  function select(link) {
    links.forEach(function (other) {
      other.classList.toggle("rt-current", other === link);
    });
  }

  var observer = new IntersectionObserver(function (entries) {
    entries.forEach(function (entry) {
      if (entry.isIntersecting && byId[entry.target.id]) {
        select(byId[entry.target.id]);
      }
    });
  }, { rootMargin: "-10% 0px -70% 0px" });

  Array.prototype.forEach.call(document.querySelectorAll(".rt-case"), function (section) {
    observer.observe(section);
  });

  select(links[0]);
})();
