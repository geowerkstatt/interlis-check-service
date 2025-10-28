import React from "react";
import ReactDOM from "react-dom";
import "bootstrap/dist/css/bootstrap.min.css";
import "./index.css";
import AppContext from "./appContext";

ReactDOM.render(
  <React.StrictMode>
    <AppContext />
  </React.StrictMode>,
  document.getElementById("root")
);
