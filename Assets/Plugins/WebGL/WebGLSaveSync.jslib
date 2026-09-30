mergeInto(LibraryManager.library, {
  WebGLSaveSync_Sync: function (populate, id) {
    FS.syncfs(!!populate, function (err) {
      SendMessage('WebGLSaveSyncReceiver', 'OnSyncComplete', id + ':' + (err ? '0' : '1'));
    });
  }
});
