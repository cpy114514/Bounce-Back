mergeInto(LibraryManager.library, {
  MenuFullscreenState: function () {
    var fullscreen = document.fullscreenElement || document.webkitFullscreenElement;
    if (fullscreen) {
      return fullscreen === Module.canvas || fullscreen.contains(Module.canvas) ? 1 : 0;
    }
    return document.fullscreenEnabled || document.webkitFullscreenEnabled ? 0 : -1;
  }
});
