#!/bin/bash
set -e

# Set proxy environment variables
[[ -n $PROXY ]] && export \
  http_proxy=$PROXY \
  https_proxy=$PROXY \
  HTTP_PROXY=$PROXY \
  HTTPS_PROXY=$PROXY

# Set default values (if not specified in docker-compose)
export ILIVALIDATOR_MODEL_DIR="%ITF_DIR;$ILITOOLS_MODELS_DIR;${ILIVALIDATOR_MODEL_DIR:-https://models.interlis.ch/}"
export DELETE_TRANSFER_FILES=${DELETE_TRANSFER_FILES:-false}
export ENABLE_GPKG_VALIDATION=${ENABLE_GPKG_VALIDATION:-false}

# Override ilitools default cache directory
export ILI_CACHE=$ILITOOLS_CACHE_DIR

# Copy custom web assets to the web app public folder
[[ "$(ls -A $ILICHECK_WEB_ASSETS_DIR)" ]] && \
  echo -n "Copy custom web assets ..." && \
  cp -f $ILICHECK_WEB_ASSETS_DIR/* $ILICHECK_APP_HOME_DIR/ClientApp/build/ && \
  echo "done!"

# Use default user:group if no $PUID and/or $PGID is provided.
groupmod -o -g ${PUID:-1654} app && \
  usermod -o -u ${PGID:-1654} app &> /dev/null

# Change owner for our folders
echo -n "Fix permissions for mounted volumes ..." && \
  chown -R app:app $ILICHECK_APP_HOME_DIR && \
  chown -R app:app $ILICHECK_APP_LOG_DIR && \
  chown -R app:app $ILICHECK_UPLOADS_DIR && \
  chown -R app:app $ILICHECK_WEB_ASSETS_DIR && \
  chown -R app:app $ILITOOLS_HOME_DIR && \
  chown -R app:app $ILITOOLS_CONFIG_DIR && \
  chown -R app:app $ILITOOLS_CATALOGUES_DIR && \
  chown -R app:app $ILITOOLS_MODELS_DIR && \
  chown -R app:app $ILITOOLS_PLUGINS_DIR && \
  chown -R app:app $ILITOOLS_CACHE_DIR && \
  echo "done!"

# Export current environment for all users and cron jobs
echo -n "Configure environment ..." && \
  env | xargs -I {} echo 'export "{}"' > /etc/profile.d/env.sh && \
  env >> /etc/environment && echo "done!"

# Setup and run cron jobs
[[ -n $TRANSFER_AND_LOG_DATA_RETENTION ]] && \
  echo -n "Setup cron jobs ..." && \
  echo '* * * * * /usr/bin/find $ILICHECK_UPLOADS_DIR -mindepth 1 -maxdepth 1 -type d -not -newermt "$TRANSFER_AND_LOG_DATA_RETENTION ago" -exec rm -r "{}" \; > /proc/1/fd/1 2>/proc/1/fd/2' | crontab - && \
  cron && echo "done!"

echo "
--------------------------------------------------------------------------
ilicheck version:                 $ILICHECK_APP_VERSION
delete transfer files:            $([[ $DELETE_TRANSFER_FILES = true ]] && echo enabled || echo disabled)
transfer and log data retention:  $([[ -n $TRANSFER_AND_LOG_DATA_RETENTION ]] && echo $TRANSFER_AND_LOG_DATA_RETENTION || echo unset)
ilivalidator version:             $ILIVALIDATOR_VERSION `[[ $ILIVALIDATOR_VERSION != $ILIVALIDATOR_LATEST_VERSION ]] && echo "(new version $ILIVALIDATOR_LATEST_VERSION available!)"`
ili2gpkg version:                 $([[ $ENABLE_GPKG_VALIDATION = false ]] && echo "not configured" || echo $ILI2GPKG_VERSION `[[ $ILI2GPKG_VERSION != $ILI2GPKG_LATEST_VERSION ]] && echo "(new version $ILI2GPKG_LATEST_VERSION available!)"`)
ilivalidator config file name:    $([[ -n $ILIVALIDATOR_CONFIG_NAME ]] && echo $ILIVALIDATOR_CONFIG_NAME || echo disabled)
ilivalidator model repositories:  $ILIVALIDATOR_MODEL_DIR
ilivalidator trace messages:      $([[ $ILIVALIDATOR_ENABLE_TRACE = true ]] && echo enabled || echo disabled)
http proxy:                       ${PROXY:-no proxy set}
http proxy exceptions:            $([[ -n $NO_PROXY ]] && echo $NO_PROXY || echo undefined)
user uid:                         $(id -u app)
user gid:                         $(id -g app)
timezone:                         $TZ
--------------------------------------------------------------------------
"

echo -e "INTERLIS web check service app is up and running!\n" && \
  sudo -H --preserve-env --user app dotnet ILICheck.Web.dll
