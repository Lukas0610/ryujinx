.PHONY: all

-include Makefile.user.mk

all: clean win-x64 linux-x64 linux-arm64 osx-arm64
release: clean win-x64-package linux-x64-package linux-arm64-package osx-arm64-package
clean: win-x64-clean linux-x64-clean linux-arm64-clean osx-arm64-clean

###########################################
## win-x64
##
win-x64: win-x64-clean
	dotnet publish \
	    -c Release \
	    -r win-x64 \
	    -o build/win-x64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:PublishTrimmed=false \
	    -p:EnableTrimAnalyzer=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

win-x64-package: win-x64-clean win-x64
	cd build/win-x64/ && zip -r9 ../Ryujinx-Windows-x64.zip .

win-x64-clean:
	rm -rvf build/win-x64
	rm -vf build/Ryujinx-Windows-x64.zip

###########################################
## linux-x64
##
linux-x64: linux-x64-clean
	dotnet publish \
	    -c Release \
	    -r linux-x64 \
	    -o build/linux-x64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:PublishTrimmed=false \
	    -p:EnableTrimAnalyzer=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

linux-x64-package: linux-x64-clean linux-x64
	cd build/linux-x64/ && tar -cvaf ../Ryujinx-Linux-x64.tgz *

linux-x64-clean:
	rm -rvf build/linux-x64
	rm -vf build/Ryujinx-Linux-x64.tgz

###########################################
## linux-arm64
##
linux-arm64: linux-arm64-clean
	dotnet publish \
	    -c Release \
	    -r linux-arm64 \
	    -o build/linux-arm64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:PublishTrimmed=false \
	    -p:EnableTrimAnalyzer=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

linux-arm64-package: linux-arm64-clean linux-arm64
	cd build/linux-arm64/ && tar -cvaf ../Ryujinx-Linux-arm64.tgz *

linux-arm64-clean:
	rm -rvf build/linux-arm64
	rm -vf build/Ryujinx-Linux-arm64.tgz

###########################################
## osx-arm64
##
osx-arm64: clean-osx-arm64
	dotnet publish \
	    -c Release \
	    -r osx-arm64 \
	    -o build/osx-arm64 \
	    -p:PublishReadyToRun=true \
	    -p:PublishSingleFile=false \
	    -p:PublishTrimmed=false \
	    -p:EnableTrimAnalyzer=false \
	    -p:DebugType=embedded \
	    --self-contained true \
	    src/Ryujinx/Ryujinx.csproj

osx-arm64-package: osx-arm64-clean osx-arm64
	cd build/osx-arm64/ && tar -cvaf ../Ryujinx-OSX-arm64.tgz *

osx-arm64-clean:
	rm -rvf build/osx-arm64
	rm -vf build/Ryujinx-OSX-arm64.tgz
