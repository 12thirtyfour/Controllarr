#!/bin/bash

outputFolder=_output
artifactsFolder=_artifacts
uiFolder="$outputFolder/UI"
framework="${FRAMEWORK:=net10.0}"

rm -rf $artifactsFolder
mkdir $artifactsFolder

for runtime in _output/*
do
  name="${runtime##*/}"
  folderName="$runtime/$framework"
  controllarrFolder="$folderName/Controllarr"
  archiveName="Controllarr.$BRANCH.$CONTROLLARR_VERSION.$name"

  if [[ "$name" == 'UI' ]]; then
    continue
  fi
    
  echo "Creating package for $name"

  echo "Copying UI"
  cp -r $uiFolder $controllarrFolder
  
  echo "Setting permissions"
  find $controllarrFolder -name "ffprobe" -exec chmod a+x {} \;
  find $controllarrFolder -name "Controllarr" -exec chmod a+x {} \;
  find $controllarrFolder -name "Controllarr.Update" -exec chmod a+x {} \;
  
  if [[ "$name" == *"osx"* ]]; then
    echo "Creating macOS package"
      
    packageName="$name-app"
    packageFolder="$outputFolder/$packageName"
      
    rm -rf $packageFolder
    mkdir $packageFolder
      
    cp -r distribution/macOS/Controllarr.app $packageFolder
    mkdir -p $packageFolder/Controllarr.app/Contents/MacOS
      
    echo "Copying Binaries"
    cp -r $controllarrFolder/* $packageFolder/Controllarr.app/Contents/MacOS
      
    echo "Removing Update Folder"
    rm -r $packageFolder/Controllarr.app/Contents/MacOS/Controllarr.Update
              
    echo "Packaging macOS app Artifact"
    (cd $packageFolder; zip -rq "../../$artifactsFolder/$archiveName-app.zip" ./Controllarr.app)
  fi

  echo "Packaging Artifact"
  if [[ "$name" == *"linux"* ]] || [[ "$name" == *"osx"* ]] || [[ "$name" == *"freebsd"* ]]; then
    tar -zcf "./$artifactsFolder/$archiveName.tar.gz" -C $folderName Controllarr
	fi
    
  if [[ "$name" == *"win"* ]]; then
    if [ "$RUNNER_OS" = "Windows" ]
      then
        (cd $folderName; 7z a -tzip "../../../$artifactsFolder/$archiveName.zip" ./Controllarr)
      else
      (cd $folderName; zip -rq "../../../$artifactsFolder/$archiveName.zip" ./Controllarr)
    fi
	fi
done
