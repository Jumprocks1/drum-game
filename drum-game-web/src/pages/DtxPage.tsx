import PageComponent from "../framework/PageComponent";
import Router, { RouteParameters } from "../framework/Router";
import GlobalData from "../GlobalData";
import MapCarousel, { CarouselState } from "../selector/MapCarousel";

import { CacheMap } from "../interfaces/Cache";
import DtxPreview from "../dtx/DtxPreview";

export default class DtxPage extends PageComponent {
    static Route = "dtx/$0|dtx/?"
    static RouteUrl = "dtx/$0"
    static PageId = "dtx-page"


    MapUrl: string | undefined;

    LoadRoute(parameters: RouteParameters) {
        this.MapUrl = parameters[0]
    }

    UrlForFile(filename: string) {
        return filename.substring(0, filename.lastIndexOf("."))
    }

    AfterParent() {
        super.AfterParent();
        const carousel = new MapCarousel();

        const preview = new DtxPreview();
        carousel.OnMapChange = e => {
            preview.SetMap(e);
            if (this.MapUrl) { // if we have a map in the URL, we make sure to keep updating the URL
                const newUrl = this.UrlForFile(e.FileName);
                if (newUrl !== this.MapUrl) {
                    const router = this.FindParent(Router);
                    this.MapUrl = newUrl;
                    router.ReplaceRoute({ page: DtxPage, parameters: [this.MapUrl] })
                }
            }
        }


        this.Add(preview);
        this.Add(carousel);

        GlobalData.DtxMapList().then(maps => {
            if (!this.Alive) return;
            const o: CacheMap[] = Object.values(maps.Maps);
            let target: CacheMap | undefined = CarouselState.map === undefined ? o[0] : undefined;
            if (this.MapUrl) {
                target = maps.Maps[this.MapUrl] ?? maps.Maps[this.MapUrl + ".bjson"]
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