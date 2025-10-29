import { useState } from "react";

export const CopyLinkToClipboard = ({ tooltipText, link, children, className }) => {
  const [tooltip, setTooltip] = useState(tooltipText);

  const copyToClipboard = () => {
    navigator.clipboard.writeText(new URL(link, window.location).href);
    setTooltip("Link wurde kopiert");
  };

  const resetToDefaultText = () => setTooltip(tooltipText);

  return (
    <div onClick={copyToClipboard} onMouseLeave={resetToDefaultText} className={className}>
      {children}
      <span className="icon-tooltip-text">{tooltip}</span>
    </div>
  );
};
