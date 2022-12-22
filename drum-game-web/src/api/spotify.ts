export function getUrl(spotifyReference: string | undefined) { // based on what we use in the C# Drum Game
    if (!spotifyReference) return;
    // I don't trust this code too much yet
    try {
        const idRegex = "([A-Za-z0-9]{15,})"; // note that both of these create capture groups
        const resRegex = "(track|album|artist)";
        const reg1 = new RegExp(`^${idRegex}$`);
        const reg2 = new RegExp(`^https?://open.spotify.com/${resRegex}/${idRegex}`);
        const reg3 = new RegExp(`^spotify:${resRegex}:${idRegex}$`);

        let match = reg1.exec(spotifyReference)
            ?? reg2.exec(spotifyReference)
            ?? reg3.exec(spotifyReference);

        if (!match) return;

        let resource = "track";
        let id = ""
        if (match.length == 3) {
            resource = match[1]
            id = match[2]
        } else {
            id = match[1]
        }

        return `https://open.spotify.com/${resource}/${id}`
    } catch (e) { console.error(e) }
}