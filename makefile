IMAGE ?= oracle
VERSION ?= dev
FOLDER := AetherLink.Worker
MOCKFOLDER := AetherLink.MockServer
SRC_DIR := /app/src/${FOLDER}
NODE_DIR := /srv/oracle
KVROCKSDIR := /kvrocks/build/kvrocks
REDIS ?= 127.0.0.1
REDIS_PORT ?= 6379

## build: docker build image 
build:
	@echo "Building Docker image..."
	@docker build -t $(IMAGE):$(VERSION) .

## init: init cluster with redis and nodes in mac docker container
init:
	$(call kvrocks,6379)
	$(call kvrocks,6380)
	$(call kvrocks,6381)
	$(call kvrocks,6382)
	$(call kvrocks,6383)
	@cp -a /app/NuGet.Config ~/.nuget/NuGet/ 
	$(call publish)
	$(call startmock)
	$(call init,node1)
	$(call init,node2)
	$(call init,node3)
	$(call init,node4)
	$(call init,node5)


## restart: restart cluster in mac docker container
restart:
	$(call publish)
	@killall -r dotnet* 
	$(call flushall,6379)
	$(call flushall,6380)
	$(call flushall,6381)
	$(call flushall,6382)
	$(call flushall,6383)
	$(call startmock)
	$(call restart,node1)
	$(call restart,node2)
	$(call restart,node3)
	$(call restart,node4)
	$(call restart,node5)
    
flushall:
	$(call flushall,${REDIS},$(REDIS_PORT))

killall:
	@killall -r dotnet || echo "Process oracle-node was not running."

GREEN  := $(shell tput -Txterm setaf 2)
YELLOW := $(shell tput -Txterm setaf 3)
WHITE  := $(shell tput -Txterm setaf 7)
CYAN   := $(shell tput -Txterm setaf 6)
RESET  := $(shell tput -Txterm sgr0)

## help: Show this help.
help: 
	@echo 'Usage:'
	@echo '  ${YELLOW}make${RESET} ${GREEN}<Target>${RESET}'
	@echo ''
	@echo 'Targets:'
	@awk 'BEGIN {FS = ":.*?## "} { \
		if (/^[a-zA-Z_-]+:.*?##.*$$/) {printf "    ${YELLOW}%-20s${GREEN}%s${RESET}\n", $$1, $$2} \
		else if (/^## .*$$/) {printf "  ${CYAN}%s${RESET}\n", substr($$1,4)} \
		}' $(MAKEFILE_LIST)

## all: build docker-push
all: build init restart help

.PHONY: build init restart help

define init
    $(eval name := ${1})
    $(eval des := ${NODE_DIR}/${name})
    $(eval cf := ${SRC_DIR}/${name}.json)
    $(eval apcf := ${SRC_DIR}/apollosettings.json)
    $(eval ds := ${des}/build)
    $(eval log := /tmp/${name})
    
    echo $(shell ifconfig -a|grep inet|grep -v 127.0.0.1|grep -v inet6|awk '{print $$2}'|tr -d "addr:") aetherlink.${name}.test.network >> /etc/hosts
    mkdir -p ${des}
    cp -r /tmp/build ${des}
    cp ${cf} ${ds}/appsettings.json
    cp ${apcf} ${ds}/apollosettings.json
    cd ${ds} && dotnet ${FOLDER}.dll &
    mkdir -p ${log}
    ln -s ${ds}/Logs ${log} 
endef

define kvrocks
    $(eval port := ${1})
    ${KVROCKSDIR} --port ${port} --dir /tmp/kvrocks${port}/ & 
endef

define restart
    $(eval name := ${1})
    $(eval des := ${NODE_DIR}/${name})
    $(eval cf := ${SRC_DIR}/${name}.json)
    $(eval apcf := ${SRC_DIR}/apollosettings.json)
    $(eval ds := ${des}/build)
    
    rm -rf ${des}
    mkdir -p ${des}
    cp -r /tmp/build ${des}
    cp ${cf} ${ds}/appsettings.json
    cp ${apcf} ${ds}/apollosettings.json
    cd ${ds} && dotnet ${FOLDER}.dll &
endef

define publish
	dotnet publish ${SRC_DIR}/${FOLDER}.csproj -c Release -o /tmp/build
endef

define startmock
	dotnet publish /app/src/${MOCKFOLDER}/${MOCKFOLDER}.csproj -c Release -o /tmp/mock/build
	cd /tmp/mock/build && dotnet ${MOCKFOLDER}.dll &
endef

define flushall
    $(eval port := ${1})
    redis-cli -p ${port} flushall
endef