import PageComponent from "../framework/PageComponent";
import { RouteParameters } from "../framework/Router";
import GlobalData from "../GlobalData";
import MapCarousel from "../selector/MapCarousel";

import dtxMaps from "../dtx.json"
import { CacheMap } from "../interfaces/Cache";
import DtxPreview from "../dtx/DtxPreview";

export default class DtxPage extends PageComponent {
    static Route = "dtx/$0|dtx/?"
    static RouteUrl = "dtx/$0"


    MapUrl: string | undefined;

    LoadRoute(parameters: RouteParameters) {
        this.MapUrl = parameters[0]
    }

    AfterParent() {
        super.AfterParent();
        const carousel = new MapCarousel();

        const preview = new DtxPreview();
        carousel.OnMapChange = e => preview.SetMap(e);


        this.Add(preview);
        this.Add(carousel);

        // TODO we should automatically build a dtx.json that we grab here
        // It can have all the converted DTX metadata (with levels and names)
        GlobalData.LoadMapList().then(maps => {
            if (!this.Alive) return;
            const cacheMaps = maps.Maps;
            const o: CacheMap[] = []
            let target: CacheMap | undefined = undefined;
            for (const e of dtxMaps.maps) {
                const cacheMap = cacheMaps[e.filename];
                cacheMap.DtxInfo = e;
                if (e.url == this.MapUrl) target = cacheMap;
                o.push(cacheMap);
            }
            carousel.SetItems(o);
            if (target !== undefined)
                carousel.HardSelect(target)
        })
    }

    AfterRemove() {
        super.AfterRemove();
        this.ChildrenAfterRemove();
    }
}