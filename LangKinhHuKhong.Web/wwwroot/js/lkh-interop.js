// LKHK JS Interop V1.9.4.7
// Blazor imports this ES module before invoking any audio/auth/upload method.
// Static import makes the dependency load+execute deterministically.
// site.js is intentionally a side-effect module, retained unchanged.
import './site.js?v=1.9.5.0';

function core(method) {
    const instance = window.lkh;
    if (!instance || typeof instance[method] !== 'function') {
        throw new Error(`[LKHK] JS core did not initialize method "${method}". Check site.js, CSP and Network errors.`);
    }
    return instance;
}
export const api = (...args) => core('api').api(...args);
export const uploadEpisode = (...args) => core('uploadEpisode').uploadEpisode(...args);
export const replaceEpisodeAudio = (...args) => core('replaceEpisodeAudio').replaceEpisodeAudio(...args);
export const uploadCover = (...args) => core('uploadCover').uploadCover(...args);
export const uploadStoryTexts = (...args) => core('uploadStoryTexts').uploadStoryTexts(...args);
export const openNotepadFile = (...args) => core('openNotepadFile').openNotepadFile(...args);
export const initNewNotepadFile = (...args) => core('initNewNotepadFile').initNewNotepadFile(...args);
export const saveNotepadFile = (...args) => core('saveNotepadFile').saveNotepadFile(...args);
export const copyNotepadContent = (...args) => core('copyNotepadContent').copyNotepadContent(...args);
export const listen = (...args) => core('listen').listen(...args);
export const stopAudio = (...args) => core('stopAudio').stopAudio(...args);
export const bindAutoNext = (...args) => core('bindAutoNext').bindAutoNext(...args);
export const reload = () => { window.location.reload(); };
