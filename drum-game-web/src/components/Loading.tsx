export default (promise?: Promise<any>) => {
    const div = <div>Loading...</div>;
    if (promise) {
        promise.then(_ => div.Component?.Kill()).catch(e => {
            console.error(e)
            div.textContent = String(e);
        })
    }
    return div
}