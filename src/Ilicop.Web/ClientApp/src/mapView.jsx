import { useEffect, useRef, useState } from "react";
import { Container, Row, Col, Card } from "react-bootstrap";
import Map from "ol/Map";
import View from "ol/View";
import TileLayer from "ol/layer/Tile";
import TileWMS from "ol/source/TileWMS";
import WMSCapabilities from "ol/format/WMSCapabilities";
import { transformExtent } from "ol/proj";
import "ol/ol.css";

export const MapView = ({ wmsUrl }) => {
  const mapRef = useRef();
  const [wmsCapabilities, setWmsCapabilities] = useState(null);

  useEffect(() => {
    const fetchCapabilities = async () => {
      const url = `${wmsUrl}?service=WMS&request=GetCapabilities`;
      const response = await fetch(url);
      const xml = await response.text();
      const parser = new WMSCapabilities();
      const result = parser.read(xml);

      setWmsCapabilities(result);
    };
    fetchCapabilities();
  }, [wmsUrl]);

  useEffect(() => {
    if (!wmsCapabilities) return;

    const layer = wmsCapabilities.Capability.Layer.Layer[0];
    if (!layer.Name) return;

    const boundingBox = layer?.EX_GeographicBoundingBox;
    const extent = boundingBox ? transformExtent(layer?.EX_GeographicBoundingBox, "EPSG:4326", "EPSG:3857") : null;

    const map = new Map({
      target: mapRef.current,
      layers: [
        new TileLayer({
          source: new TileWMS({
            url: wmsUrl,
            params: { LAYERS: layer.Name },
          }),
        }),
      ],
      view: new View({
        extent: extent,
      }),
    });

    if (extent) map.getView().fit(extent, { size: map.getSize() });

    return () => map.setTarget(null);
  }, [wmsCapabilities, wmsUrl]);

  return (
    <Container>
      <Row>
        <Col>
          <Card>
            <div ref={mapRef} style={{ width: "100%", height: "500px" }} />
          </Card>
        </Col>
      </Row>
    </Container>
  );
};

export default MapView;
