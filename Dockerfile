FROM mcr.microsoft.com/dotnet/sdk:7.0.306

RUN apt-get update
RUN apt-get -qqy install make
RUN apt-get -qqy install redis-server
RUN apt-get -qqy install net-tools
RUN apt-get -qqy install procps
RUN apt-get -qqy install git  
RUN apt-get -qqy install python3 
RUN apt-get -qqy install libssl-dev
RUN apt-get -qqy install build-essential 
RUN apt-get -qqy install libtool
RUN apt-get -qqy install cmake
RUN apt-get -qqy install redis-tools
RUN git clone https://github.com/apache/kvrocks.git 
RUN cd kvrocks && ./x.py build