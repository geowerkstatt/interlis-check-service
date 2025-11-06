import ReactMarkdown from "react-markdown";
import { Modal, Button } from "react-bootstrap";
import { useTranslation } from "react-i18next";

export const ModalContent = (props) => {
  const { content, type } = props;
  const { t } = useTranslation();

  return (
    <Modal {...props} size="lg" aria-labelledby="contained-modal-title-vcenter" centered>
      <div style={{ maxHeight: "calc(100vh - 58px)", display: "flex", flexDirection: "column" }}>
        <Modal.Body style={{ flex: 1, overflowY: "auto" }}>
          {type === "markdown" && <ReactMarkdown linkTarget="_blank" children={content || ""} />}
          {type === "raw" && content}
        </Modal.Body>
        <Modal.Footer style={{ flex: "0 0 auto" }}>
          <Button variant="outline-dark" onClick={props.onHide}>
            {t("common.close")}
          </Button>
        </Modal.Footer>
      </div>
    </Modal>
  );
};

export default ModalContent;
